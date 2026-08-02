using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Клиент защищённой очереди уведомлений API.
/// </summary>
public sealed class TelegramOutboxClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramOutboxClient> _logger;

    public TelegramOutboxClient(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramOutboxClient> logger)
    {
        var apiUrl = configuration["BotSettings:ApiUrl"] ?? "https://localhost:7001";
        var apiKey = Environment.GetEnvironmentVariable("BOT_SERVICE_AUTH_KEY")
                     ?? configuration["BotAuth:ApiKey"]
                     ?? configuration["BotSettings:ApiKey"];

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SugarGuard-Bot/1.0");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bot-Auth", apiKey);
        }

        _logger = logger;
    }

    public async Task<IReadOnlyList<TelegramOutboxMessage>> ClaimAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("/api/bot-service/telegram-outbox?limit=20", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Не удалось забрать очередь Telegram: {StatusCode}", response.StatusCode);
            return [];
        }

        return await response.Content.ReadFromJsonAsync<List<TelegramOutboxMessage>>(JsonSerializerOptions.Web, cancellationToken) ?? [];
    }

    public async Task CompleteAsync(Guid messageId, bool delivered, string? error, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/bot-service/telegram-outbox/{messageId}/delivery",
            new TelegramOutboxDeliveryRequest { Delivered = delivered, Error = error },
            JsonSerializerOptions.Web,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Не удалось подтвердить доставку Telegram-сообщения {MessageId}: {StatusCode}", messageId, response.StatusCode);
        }
    }

    public async Task MarkPartDeliveredAsync(Guid messageId, string part, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"/api/bot-service/telegram-outbox/{messageId}/delivery/{part}",
            content: null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Не удалось сохранить этап доставки Telegram-сообщения {MessageId}: {StatusCode}", messageId, response.StatusCode);
        }
    }

    public async Task<bool> AcknowledgeAsync(Guid messageId, long telegramUserId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/bot-service/telegram-outbox/{messageId}/acknowledgement",
            new { telegramUserId },
            JsonSerializerOptions.Web,
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Проверяет доступность управляющего API без обращения к Telegram.
    /// Этот маршрут у бота идёт напрямую, поэтому диагностирует доступность
    /// SugarGuard даже при недоступном VPN-подключении Happ.
    /// </summary>
    public async Task<bool> IsControlPlaneAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/health/live", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Проверка доступности управляющего API Telegram-бота превысила время ожидания.");
            return false;
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Не удалось проверить доступность управляющего API Telegram-бота.");
            return false;
        }
    }

    /// <summary>Передаёт API актуальное состояние связи Telegram-бота.</summary>
    public async Task<bool> ReportHeartbeatAsync(BotHeartbeatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/api/bot-service/status/heartbeat",
                request,
                JsonSerializerOptions.Web,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning("Не удалось передать heartbeat Telegram-бота: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Не удалось передать heartbeat Telegram-бота.");
            return false;
        }
    }
}

public sealed class TelegramOutboxMessage
{
    public Guid MessageId { get; set; }
    public long TelegramUserId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public bool TextDelivered { get; set; }
    public bool LocationDelivered { get; set; }
}

public sealed class TelegramOutboxDeliveryRequest
{
    public bool Delivered { get; set; }
    public string? Error { get; set; }
}

public sealed class BotHeartbeatRequest
{
    public string BotName { get; set; } = "telegram";
    public bool InternetAvailable { get; set; }
    public bool ExternalApiAvailable { get; set; }
    public string? Error { get; set; }
    public string? Version { get; set; }
}
