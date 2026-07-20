using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Системная статистика для панели администратора.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/system")]
public sealed class AdminSystemController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IServerMetricsService _serverMetricsService;
    private readonly IHealthService _healthService;
    private readonly IGigaChatUsageService _gigaChatUsageService;
    private readonly ILogger<AdminSystemController> _logger;

    public AdminSystemController(
        IAdminService adminService,
        IServerMetricsService serverMetricsService,
        IHealthService healthService,
        IGigaChatUsageService gigaChatUsageService,
        ILogger<AdminSystemController> logger)
    {
        _adminService = adminService;
        _serverMetricsService = serverMetricsService;
        _healthService = healthService;
        _gigaChatUsageService = gigaChatUsageService;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminSystemStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _adminService.GetSystemStatsAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось получить системную статистику.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("server-metrics")]
    public async Task<ActionResult<ServerMetricsResponse>> GetServerMetrics(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _serverMetricsService.GetSnapshotAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось получить метрики сервера.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("gigachat-usage")]
    public async Task<ActionResult<GigaChatUsageResponse>> GetGigaChatUsage(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _gigaChatUsageService.GetAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось получить расход токенов GigaChat.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _healthService.CanConnectAsync(cancellationToken);
            return Ok(new
            {
                status = canConnect ? "healthy" : "degraded",
                database = canConnect ? "ok" : "unavailable",
                serverUtc = DateTime.UtcNow
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Проверка здоровья завершилась с ошибкой.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                database = "error",
                serverUtc = DateTime.UtcNow
            });
        }
    }
}
