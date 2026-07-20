using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SugarGuard.MaxBot.Abstractions;
using SugarGuard.MaxBot.Models;

namespace SugarGuard.MaxBot.Services;

/// <summary>Клиент официального API MAX без логики SugarGuard и доступа к БД.</summary>
public sealed class MaxBotClient : IMaxBotClient
{
    public const string HttpClientName = "MaxBotApi";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MaxBotClient> _logger;
    private readonly MaxBotOptions _options;

    public MaxBotClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MaxBotClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = new MaxBotOptions
        {
            BotToken = configuration["Max:BotToken"] ?? Environment.GetEnvironmentVariable("MAX_BOT_TOKEN"),
            WebhookUrl = configuration["Max:WebhookUrl"] ?? Environment.GetEnvironmentVariable("MAX_WEBHOOK_URL"),
            WebhookSecret = configuration["Max:WebhookSecret"] ?? Environment.GetEnvironmentVariable("MAX_WEBHOOK_SECRET"),
            PublicBotUrl = configuration["Max:PublicBotUrl"] ?? Environment.GetEnvironmentVariable("MAX_PUBLIC_BOT_URL")
        };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BotToken)
        && !string.IsNullOrWhiteSpace(_options.WebhookUrl)
        && !string.IsNullOrWhiteSpace(_options.WebhookSecret);

    public string? PublicBotUrl => _options.PublicBotUrl;

    public async Task SendTextAsync(long maxUserId, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            throw new InvalidOperationException("Токен MAX-бота не настроен в конфигурации.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"messages?user_id={maxUserId}")
        {
            Content = JsonContent.Create(new { text = message[..Math.Min(message.Length, 4000)], format = "markdown" })
        };
        request.Headers.TryAddWithoutValidation("Authorization", _options.BotToken);
        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Ошибка MAX API: {(int)response.StatusCode} {error}");
        }
    }

    public async Task RegisterWebhookAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Webhook MAX не зарегистрирован: отсутствует токен, URL или секрет.");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "subscriptions")
        {
            Content = JsonContent.Create(new { url = _options.WebhookUrl, update_types = new[] { "bot_started", "message_created" }, secret = _options.WebhookSecret })
        };
        request.Headers.TryAddWithoutValidation("Authorization", _options.BotToken);
        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Webhook MAX зарегистрирован. Адрес={WebhookUrl}", _options.WebhookUrl);
    }
}
