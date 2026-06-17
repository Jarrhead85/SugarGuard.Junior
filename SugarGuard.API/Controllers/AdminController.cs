using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Админ контроллер
/// </summary>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    /// <summary>
    /// Инициализация
    /// </summary>
    public AdminController(
        IAdminService adminService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    // GET api/admin/invitations
    /// <summary>
    /// Возвращает список инвайт-кодов с опциональной фильтрацией по статусу
    /// </summary>
    [HttpGet("invitations")]
    [ProducesResponseType(typeof(IReadOnlyList<InviteCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetInvitationsAsync(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _adminService.GetInvitationsAsync(status, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetInvitations: ошибка при получении списка инвайт-кодов. Status={Status}.",
                status);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить список инвайт-кодов." });
        }
    }

    // POST api/admin/invitations
    /// <summary>
    /// Создаёт новый инвайт-код для указанной роли. TTL — 48 часов.
    /// </summary>
    [HttpPost("invitations")]
    [ProducesResponseType(typeof(InviteCodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateInvitationAsync(
        [FromBody] CreateAdminInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new { error = "validation_error", details = errors });
        }

        try
        {
            var result = await _adminService.CreateInvitationAsync(
                request.ChildId, request.TargetRole, cancellationToken);

            return CreatedAtAction(
                actionName:  nameof(GetInvitationsAsync),
                routeValues: null,
                value:       result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_argument", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CreateInvitation: ошибка при создании инвайт-кода. Role={Role}.",
                request.TargetRole);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось создать инвайт-код." });
        }
    }

    // DELETE api/admin/invitations/{id}
    /// <summary>
    /// Отзывает инвайт-код по его ID
    /// </summary>
    [HttpDelete("invitations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RevokeInvitationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _adminService.RevokeInvitationAsync(id, cancellationToken);

            if (result is null)
                return NotFound(new { error = "not_found", message = "Инвайт-код не найден." });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "cannot_revoke", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RevokeInvitation: ошибка при отзыве инвайт-кода. Id={Id}.", id);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось отозвать инвайт-код." });
        }
    }

    // GET api/admin/audit-logs
    /// <summary>
    /// Возвращает постраничный список записей аудит-лога с фильтрацией
    /// </summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAuditLogsAsync(
        [FromQuery] Guid?     actorUserId,
        [FromQuery] string?   action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int       limit = 200,
        CancellationToken     cancellationToken = default)
    {
        try
        {
            var result = await _adminService.GetAuditLogsAsync(
                actorUserId, action, from, to, limit, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetAuditLogs: ошибка. ActorUserId={ActorUserId} Action={Action} From={From} To={To}.",
                actorUserId, action, from, to);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить записи аудит-лога." });
        }
    }

    // GET api/admin/onboarding/funnel
    /// <summary>
    /// Возвращает аналитику воронки онбординга
    /// </summary>
    [HttpGet("onboarding/funnel")]
    [ProducesResponseType(typeof(OnboardingFunnelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOnboardingFunnelAsync(
        [FromQuery] string?   role,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken     cancellationToken = default)
    {
        try
        {
            var result = await _adminService.GetOnboardingFunnelAsync(
                role, from, to, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_argument", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetOnboardingFunnel: ошибка. Role={Role} From={From} To={To}.",
                role, from, to);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить данные воронки онбординга." });
        }
    }
}
