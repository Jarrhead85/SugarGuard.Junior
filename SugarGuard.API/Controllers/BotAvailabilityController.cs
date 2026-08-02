using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Публичный для авторизованных пользователей статус каналов доставки.
/// Техническая диагностика остаётся доступной только администраторам.
/// </summary>
[Authorize]
[ApiController]
[Route("api/bot-service")]
[Produces("application/json")]
public sealed class BotAvailabilityController : ControllerBase
{
    private static readonly TimeSpan HeartbeatFreshness = TimeSpan.FromMinutes(2);
    private readonly AppDbContext _db;

    public BotAvailabilityController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Возвращает актуальность Telegram-бота без раскрытия внутренней инфраструктуры.
    /// </summary>
    [HttpGet("telegram-availability")]
    [ProducesResponseType(typeof(TelegramBotAvailabilityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TelegramBotAvailabilityResponse>> GetTelegramAvailability(
        CancellationToken cancellationToken)
    {
        var heartbeat = await _db.BotServiceHeartbeats
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.BotName == "telegram",
                cancellationToken);

        var now = DateTime.UtcNow;
        var lastExternalSuccessIsFresh = heartbeat?.LastExternalApiSuccessAt >= now - HeartbeatFreshness;
        var isAvailable = heartbeat is not null
            && heartbeat.LastHeartbeatAt >= now - HeartbeatFreshness
            && heartbeat.InternetAvailable
            && lastExternalSuccessIsFresh
            && string.IsNullOrWhiteSpace(heartbeat.LastError);

        return Ok(new TelegramBotAvailabilityResponse
        {
            IsAvailable = isAvailable,
            LastCheckedAt = heartbeat?.LastHeartbeatAt,
            Message = isAvailable
                ? "Telegram-бот работает."
                : "Telegram-бот временно недоступен. Мы уже восстанавливаем подключение; данные SugarGuard продолжают работать."
        });
    }
}
