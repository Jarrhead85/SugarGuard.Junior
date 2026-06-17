using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Дашборд: сводка и история измерений ребёнка
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<DashboardController> _logger;

    /// <summary>
    /// Инициализирует контроллер дашборда
    /// </summary>
    public DashboardController(
        IDashboardService dashboardService,
        IChildAccessService childAccess,
        ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _childAccess = childAccess;
        _logger = logger;
    }

    // GET api/dashboard/{childId}/summary
    /// <summary>
    /// Возвращает сводку по ребёнку: последнее измерение, счётчики событий,
    /// количество ожидающих экспортов и неразрешенных конфликтов синхронизации
    /// </summary>
    [HttpGet("{childId:guid}/summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSummary(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _dashboardService.ChildExistsAsync(childId, cancellationToken))
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Child not found", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var summary = await _dashboardService.GetSummaryAsync(childId, cancellationToken);

            _logger.LogDebug(
                "GetSummary: ChildId={ChildId} Total={Total} Critical={Critical}.",
                childId, summary.TotalMeasurements, summary.CriticalEvents);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetSummary: ошибка при формировании сводки. ChildId={ChildId}.", childId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить сводку." });
        }
    }

    // GET api/dashboard/{childId}/history
    /// <summary>
    /// Возвращает историю измерений ребёнка с опциональной фильтрацией по периоду
    /// </summary>
    [HttpGet("{childId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<DashboardHistoryItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistory(
        Guid childId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? uiState,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!await _dashboardService.ChildExistsAsync(childId, cancellationToken))
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Child not found", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var response = await _dashboardService.GetHistoryAsync(
                childId, from, to, uiState, limit, cancellationToken);

            _logger.LogDebug(
                "GetHistory: ChildId={ChildId} From={From} To={To} UiState={UiState} ResultRows={ResultRows}.",
                childId, from, to, uiState, response.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetHistory: ошибка при получении истории. ChildId={ChildId}.", childId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить историю измерений." });
        }
    }
}
