using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Data;

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
    private readonly IMaxBotService _maxBotService;
    private readonly ILogger<AdminSystemController> _logger;
    private readonly AppDbContext _db;

    public AdminSystemController(
        IAdminService adminService,
        IServerMetricsService serverMetricsService,
        IHealthService healthService,
        IGigaChatUsageService gigaChatUsageService,
        IMaxBotService maxBotService,
        ILogger<AdminSystemController> logger,
        AppDbContext db)
    {
        _adminService = adminService;
        _serverMetricsService = serverMetricsService;
        _healthService = healthService;
        _gigaChatUsageService = gigaChatUsageService;
        _maxBotService = maxBotService;
        _logger = logger;
        _db = db;
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

    /// <summary>Состояние Telegram-бота и его очереди доставки.</summary>
    [HttpGet("bots")]
    public async Task<ActionResult<IReadOnlyList<BotRuntimeStatusResponse>>> GetBots(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var heartbeats = await _db.BotServiceHeartbeats
            .AsNoTracking()
            .OrderBy(item => item.BotName)
            .ToListAsync(cancellationToken);

        var pending = await _db.TelegramOutboxMessages
            .CountAsync(item => item.DeliveredAt == null && item.FailedAt == null, cancellationToken);
        var failed = await _db.TelegramOutboxMessages
            .CountAsync(item => item.FailedAt != null, cancellationToken);

        var statuses = heartbeats.Select(item => new BotRuntimeStatusResponse
        {
            BotName = item.BotName,
            IsConfigured = true,
            IsOnline = item.LastHeartbeatAt >= now.AddMinutes(-2),
            InternetAvailable = item.InternetAvailable,
            StatusMessage = item.LastHeartbeatAt >= now.AddMinutes(-2)
                ? null
                : "Нет актуального heartbeat.",
            LastHeartbeatAt = item.LastHeartbeatAt,
            LastExternalApiSuccessAt = item.LastExternalApiSuccessAt,
            LastError = item.LastError,
            Version = item.Version,
            PendingTelegramMessages = string.Equals(item.BotName, "telegram", StringComparison.OrdinalIgnoreCase) ? pending : 0,
            FailedTelegramMessages = string.Equals(item.BotName, "telegram", StringComparison.OrdinalIgnoreCase) ? failed : 0
        }).ToList();

        // MAX пока развёрнут в составе API, а не отдельным worker-процессом.
        // Поэтому честно показываем его конфигурацию, не выдавая её за проверку связи.
        if (!statuses.Any(item => string.Equals(item.BotName, "max", StringComparison.OrdinalIgnoreCase)))
        {
            statuses.Add(new BotRuntimeStatusResponse
            {
                BotName = "max",
                IsConfigured = _maxBotService.IsConfigured,
                IsOnline = false,
                InternetAvailable = false,
                StatusMessage = _maxBotService.IsConfigured
                    ? "MAX настроен; ожидается отдельный worker для runtime-проверки."
                    : "MAX-бот не настроен."
            });
        }

        return Ok(statuses);
    }
}
