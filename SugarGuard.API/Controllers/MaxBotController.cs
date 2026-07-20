using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.Shared.Constants;
using SugarGuard.Shared.Validation;

namespace SugarGuard.API.Controllers;

/// <summary>Защищённый webhook официального MAX Bot API.</summary>
[ApiController]
[Route("api/max")]
public sealed class MaxBotController : ControllerBase
{
    private readonly IMaxBotService _maxBot;
    private readonly IParentLinkService _parentLink;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaxBotController> _logger;

    public MaxBotController(IMaxBotService maxBot, IParentLinkService parentLink, IConfiguration configuration, ILogger<MaxBotController> logger)
    {
        _maxBot = maxBot;
        _parentLink = parentLink;
        _configuration = configuration;
        _logger = logger;
    }

    [Authorize(Policy = "ParentOrDoctorOrAdmin")]
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { configured = _maxBot.IsConfigured, botUrl = _maxBot.PublicBotUrl });

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] MaxUpdate update, CancellationToken cancellationToken)
    {
        var expectedSecret = _configuration["Max:WebhookSecret"] ?? Environment.GetEnvironmentVariable("MAX_WEBHOOK_SECRET");
        if (string.IsNullOrWhiteSpace(expectedSecret) || !Request.Headers.TryGetValue("X-Max-Bot-Api-Secret", out var suppliedSecret) || !SecretsMatch(expectedSecret, suppliedSecret!))
        {
            return Unauthorized();
        }

        try
        {
            var user = update.User ?? update.Message?.Sender;
            if (user is null || user.UserId <= 0)
            {
                return Ok();
            }

            if (string.Equals(update.UpdateType, "bot_started", StringComparison.OrdinalIgnoreCase))
            {
                await _maxBot.SendTextAsync(user.UserId, WelcomeText(), cancellationToken);
                return Ok();
            }

            if (!string.Equals(update.UpdateType, "message_created", StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            var text = update.Message?.Body?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                await _maxBot.SendTextAsync(user.UserId, "Поддерживаются команды /start, /help и /connect <код>.", cancellationToken);
                return Ok();
            }

            await HandleCommandAsync(user, text, cancellationToken);
            return Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "MAX webhook processing failed. UpdateType={UpdateType}", update.UpdateType);
            // MAX повторяет запросы при не-200; сообщение уже могло быть обработано.
            return Ok();
        }
    }

    private async Task HandleCommandAsync(MaxUser user, string text, CancellationToken cancellationToken)
    {
        var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        switch (command)
        {
            case "/start":
                await _maxBot.SendTextAsync(user.UserId, WelcomeText(), cancellationToken);
                return;
            case "/help":
                await _maxBot.SendTextAsync(user.UserId, HelpText(), cancellationToken);
                return;
            case "/connect":
                await ConnectAsync(user, text, cancellationToken);
                return;
            default:
                await _maxBot.SendTextAsync(user.UserId, "Неизвестная команда. Используйте /help.", cancellationToken);
                return;
        }
    }

    private async Task ConnectAsync(MaxUser user, string text, CancellationToken cancellationToken)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !ConnectionCodeFormat.IsValid(parts[1], normalize: true))
        {
            await _maxBot.SendTextAsync(user.UserId, $"Неверный формат. Используйте: /connect {ConnectionCodeFormat.Format(ConnectionCodeFormat.Generate())}", cancellationToken);
            return;
        }

        var result = await _parentLink.VerifyMaxConnectionCodeAsync(new VerifyMaxConnectionCodeRequest
        {
            ConnectionCode = ConnectionCodeFormat.Normalize(parts[1])!,
            MaxUserId = user.UserId,
            MaxUsername = user.Username
        }, cancellationToken);

        await _maxBot.SendTextAsync(user.UserId,
            result.IsValid
                ? "✅ MAX подключён. Теперь вы будете получать уведомления SugarGuard: измерения, перекусы, критические алерты и ежедневные сводки."
                : "❌ Код недействителен или истёк. Получите новый код в настройках родительского кабинета и повторите попытку.",
            cancellationToken);
    }

    private static bool SecretsMatch(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static string WelcomeText() => "Добро пожаловать в SugarGuard MAX. Для привязки откройте настройки ребёнка в родительском кабинете, получите код и отправьте /connect <код>. Используйте /help для справки.";
    private static string HelpText() => "Команды SugarGuard MAX:\n/start — приветствие\n/help — справка\n/connect ABCD-2345 — привязать уведомления к ребёнку.\n\nПосле привязки бот отправляет измерения, перекусы, критические алерты с картой и ежедневную сводку.";
}

public sealed class MaxUpdate
{
    [JsonPropertyName("update_type")] public string? UpdateType { get; init; }
    [JsonPropertyName("user")] public MaxUser? User { get; init; }
    [JsonPropertyName("message")] public MaxMessage? Message { get; init; }
}

public sealed class MaxUser
{
    [JsonPropertyName("user_id")] public long UserId { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
}

public sealed class MaxMessage
{
    [JsonPropertyName("sender")] public MaxUser? Sender { get; init; }
    [JsonPropertyName("body")] public MaxMessageBody? Body { get; init; }
}

public sealed class MaxMessageBody
{
    [JsonPropertyName("text")] public string? Text { get; init; }
}
