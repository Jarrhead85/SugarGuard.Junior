using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Filters;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Controllers;

/// <summary>Принимает сигналы работоспособности от внешних ботов.</summary>
[BotServiceApiKey]
[AllowAnonymous]
[ApiController]
[Route("api/bot-service/status")]
[Produces("application/json")]
public sealed class BotServiceStatusController : ControllerBase
{
    private readonly AppDbContext _db;

    public BotServiceStatusController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(
        [FromBody] BotHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var botName = request.BotName?.Trim();
        if (string.IsNullOrWhiteSpace(botName) || botName.Length > 64)
        {
            return ValidationProblem("Укажите корректное имя бота.");
        }

        var heartbeat = await _db.BotServiceHeartbeats
            .SingleOrDefaultAsync(item => item.BotName == botName, cancellationToken);

        if (heartbeat is null)
        {
            heartbeat = new BotServiceHeartbeat { BotName = botName };
            _db.BotServiceHeartbeats.Add(heartbeat);
        }

        heartbeat.LastHeartbeatAt = DateTime.UtcNow;
        heartbeat.InternetAvailable = request.InternetAvailable;
        heartbeat.LastError = string.IsNullOrWhiteSpace(request.Error)
            ? null
            : request.Error.Trim()[..Math.Min(1000, request.Error.Trim().Length)];
        heartbeat.Version = string.IsNullOrWhiteSpace(request.Version)
            ? null
            : request.Version.Trim()[..Math.Min(80, request.Version.Trim().Length)];

        if (request.ExternalApiAvailable)
        {
            heartbeat.LastExternalApiSuccessAt = heartbeat.LastHeartbeatAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
