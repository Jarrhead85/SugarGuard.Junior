using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.Application.Dashboard;
using SugarGuard.API.Services;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Кабинет родителя — аналитика по ребёнку
/// </summary>
[Authorize]
[ApiController]
[Route("api/parentdashboard")]
[Produces("application/json")]
public class ParentController : ControllerBase
{
    private readonly IParentDashboardService _dashboard;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<ParentController> _logger;

    /// <summary>
    /// Инициализирует контроллер кабинета родителя
    /// </summary>
    public ParentController(
        IParentDashboardService dashboard,
        IChildAccessService childAccess,
        ILogger<ParentController> logger)
    {
        _dashboard = dashboard;
        _childAccess = childAccess;
        _logger = logger;
    }

    // GET api/parentdashboard/{childId}/summary
    /// <summary>
    /// Возвращает агрегированную сводку KPI за последние 24 часа
    /// </summary>
    [HttpGet("{childId:guid}/summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSummaryAsync(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetSummary: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _childAccess.GetCurrentUserId(), childId);

            return Forbid();
        }

        try
        {
            var summary = await _dashboard.GetSummaryAsync(childId, cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetSummary: ошибка при получении сводки. ChildId={ChildId}.", childId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить сводку." });
        }
    }

    // GET api/parentdashboard/{childId}/statistics
    /// <summary>
    /// Возвращает статистику за произвольный период
    /// </summary>
    [HttpGet("{childId:guid}/statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStatisticsAsync(
        Guid childId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var periodFrom = from ?? DateTime.UtcNow.AddDays(-7);
        var periodTo = to ?? DateTime.UtcNow;

        if (periodFrom >= periodTo)
        {
            return BadRequest(new
            {
                error = "invalid_period",
                message = "Параметр 'from' должен быть раньше 'to'."
            });
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetStatistics: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _childAccess.GetCurrentUserId(), childId);

            return Forbid();
        }

        try
        {
            var stats = await _dashboard.GetPeriodStatisticsAsync(
                childId, periodFrom, periodTo, cancellationToken);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetStatistics: ошибка. ChildId={ChildId} From={From} To={To}.",
                childId, periodFrom, periodTo);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить статистику." });
        }
    }

    // GET api/parentdashboard/{childId}/comparison
    /// <summary>
    /// Возвращает сравнение двух периодов одинаковой длины — текущего и предыдущего
    /// </summary>
    [HttpGet("{childId:guid}/comparison")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetComparisonAsync(
        Guid childId,
        [FromQuery] string period = "week",
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Определяем границы текущего периода по строковому параметру
        var (currentFrom, currentTo) = period.ToLowerInvariant() switch
        {
            "week" => (now.AddDays(-7), now),
            "month" => (now.AddDays(-30), now),
            _ => ((DateTime?)null, (DateTime?)null)
        };

        if (currentFrom is null)
        {
            return BadRequest(new
            {
                error = "invalid_period",
                message = "Параметр 'period' должен быть 'week' или 'month'."
            });
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetComparison: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _childAccess.GetCurrentUserId(), childId);

            return Forbid();
        }

        try
        {
            var comparison = await _dashboard.GetPeriodComparisonAsync(
                childId,
                currentFrom.Value,
                currentTo!.Value,
                cancellationToken);

            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetComparison: ошибка. ChildId={ChildId} Period={Period}.",
                childId, period);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить сравнение периодов." });
        }
    }

    // GET api/parentdashboard/{childId}/timeline
    /// <summary>
    /// Возвращает хронологическую ленту событий за период
    /// </summary>

    [HttpGet("{childId:guid}/timeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTimelineAsync(
        Guid childId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var periodFrom = from ?? DateTime.UtcNow.AddHours(-24);
        var periodTo = to ?? DateTime.UtcNow;

        if (periodFrom >= periodTo)
        {
            return BadRequest(new
            {
                error = "invalid_period",
                message = "Параметр 'from' должен быть раньше 'to'."
            });
        }

        var safeLimit = Math.Clamp(limit, 1, 500);

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetTimeline: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _childAccess.GetCurrentUserId(), childId);

            return Forbid();
        }

        try
        {
            var events = await _dashboard.GetTimelineAsync(
                childId, periodFrom, periodTo, safeLimit, cancellationToken);

            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetTimeline: ошибка. ChildId={ChildId} From={From} To={To} Limit={Limit}.",
                childId, periodFrom, periodTo, safeLimit);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить ленту событий." });
        }
    }

    // GET api/parentdashboard/{childId}/patterns
    /// <summary>
    /// Автоматически обнаруживает паттерны гипо/гипер-эпизодов за последние 14 дней
    /// </summary>
    [HttpGet("{childId:guid}/patterns")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPatternsAsync(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetPatterns: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _childAccess.GetCurrentUserId(), childId);

            return Forbid();
        }

        try
        {
            var patterns = await _dashboard.DetectPatternsAsync(childId, cancellationToken);
            return Ok(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetPatterns: ошибка. ChildId={ChildId}.", childId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить паттерны." });
        }
    }
}
