using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Управление логами синхронизации и разрешением конфликтов MAUI - сервер
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/sync-logs")]
[Produces("application/json")]
public class SyncLogsController : ControllerBase
{
    private readonly ISyncLogService _syncLogService;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<SyncLogsController> _logger;

    public SyncLogsController(
        ISyncLogService syncLogService,
        IChildAccessService childAccess,
        ILogger<SyncLogsController> logger)
    {
        _syncLogService = syncLogService;
        _childAccess = childAccess;
        _logger = logger;
    }

    // GET api/sync-logs
    /// <summary>
    /// Возвращает список логов синхронизации с опциональной фильтрацией
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SyncLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<SyncLogResponse>>> Get(
        [FromQuery] Guid? childId,
        [FromQuery] bool? onlyConflicts,
        [FromQuery] DateTime? since,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyCollection<Guid>? childIds = null;

            if (childId.HasValue)
            {
                if (!await _childAccess.CanAccessChildAsync(childId.Value, cancellationToken))
                    return Forbid();

                childIds = new[] { childId.Value };
            }
            else
            {
                var accessible = await _childAccess.GetAccessibleChildIdsAsync(cancellationToken);
                if (accessible.Count == 0)
                    return Ok(Array.Empty<SyncLogResponse>());

                childIds = accessible;
            }

            var items = await _syncLogService.GetAsync(
                childIds,
                onlyConflicts == true,
                limit,
                since,
                cancellationToken);

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Get: ошибка при получении логов синхронизации. ChildId={ChildId}.", childId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить логи синхронизации." });
        }
    }

    // POST api/sync-logs/{id}/resolve
    /// <summary>
    /// Разрешает конфликт синхронизации вручную
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(SyncLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SyncLogResponse>> Resolve(
        Guid id,
        [FromBody] ResolveSyncConflictRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Resolution is not ("useServer" or "useClient"))
        {
            return this.ProblemWithCode(400, "Invalid Resolution",
                "Допустимые значения resolution: useServer, useClient.", "invalid_resolution");
        }

        try
        {
            var (log, status) = await _syncLogService.ResolveAsync(
                id, request.Resolution, cancellationToken);

            switch (status)
            {
                case ResolveOneStatus.NotFound:
                    return this.ProblemWithCode(404, "Sync Log Not Found",
                        "Запись лога синхронизации не найдена.", "sync_log_not_found");

                case ResolveOneStatus.NotAConflict:
                    return this.ProblemWithCode(400, "Not A Conflict",
                        "Данная запись не является конфликтом и не требует разрешения.", "not_a_conflict");

                case ResolveOneStatus.Success:
                    if (log is null)
                    {
                        throw new InvalidOperationException(
                            "ResolveAsync returned Success but log is null.");
                    }

                    if (!await _childAccess.CanAccessChildAsync(log.ChildId, cancellationToken))
                        return Forbid();

                    return Ok(MapToResponse(log));

                default:
                    throw new InvalidOperationException($"Unknown status: {status}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Resolve: ошибка при разрешении конфликта. SyncLogId={SyncLogId}.", id);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось разрешить конфликт синхронизации." });
        }
    }

    private static SyncLogResponse MapToResponse(SyncLog log) => new()
    {
        SyncLogId = log.SyncLogId,
        ChildId = log.ChildId,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Status = log.Status,
        IsConflict = log.IsConflict,
        Error = log.Error,
        ResolutionSource = log.ResolutionSource,
        CreatedAt = log.CreatedAt
    };

    // POST api/sync-logs/resolve-all
    /// <summary>
    /// Разрешает все доступные пользователю конфликты синхронизации
    /// </summary>
    [HttpPost("resolve-all")]
    [ProducesResponseType(typeof(ResolveAllResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResolveAllResult>> ResolveAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var accessibleChildIds = await _childAccess.GetAccessibleChildIdsAsync(cancellationToken);

            var resolvedCount = await _syncLogService.ResolveAllAsync(
                accessibleChildIds, cancellationToken);

            _logger.LogInformation(
                "ResolveAll: разрешено {Count} конфликтов.",
                resolvedCount);

            return Ok(new ResolveAllResult { ResolvedCount = resolvedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResolveAll: ошибка.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось разрешить конфликты." });
        }
    }
}

/// <summary>
/// Результат операции ResolveAll
/// </summary>
public sealed class ResolveAllResult
{   
    public int ResolvedCount { get; set; } // Количество разрешённых конфликтов
}
