using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Services;

/// <summary>Реальный клиент официального MAX Bot API.</summary>
public sealed class MaxBotService : IMaxBotService
{
    public const string HttpClientName = "MaxBotApi";
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaxBotService> _logger;

    public MaxBotService(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MaxBotService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string? Token => _configuration["Max:BotToken"] ?? Environment.GetEnvironmentVariable("MAX_BOT_TOKEN");
    private string? WebhookUrl => _configuration["Max:WebhookUrl"] ?? Environment.GetEnvironmentVariable("MAX_WEBHOOK_URL");
    private string? WebhookSecret => _configuration["Max:WebhookSecret"] ?? Environment.GetEnvironmentVariable("MAX_WEBHOOK_SECRET");
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(WebhookUrl) && !string.IsNullOrWhiteSpace(WebhookSecret);
    public string? PublicBotUrl => _configuration["Max:PublicBotUrl"] ?? Environment.GetEnvironmentVariable("MAX_PUBLIC_BOT_URL");

    public Task<NotificationResponse> SendMeasurementNotificationAsync(MeasurementNotificationRequest request, CancellationToken cancellationToken = default) =>
        SendToParentsAsync(request.ChildId, $"📊 Новое измерение\nГлюкоза: {request.GlucoseValue:0.0} ммоль/л\nСтатус: {request.Status}\nВремя: {request.MeasurementTime.ToLocalTime():dd.MM HH:mm}", cancellationToken);

    public Task<NotificationResponse> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request, CancellationToken cancellationToken = default) =>
        SendToParentsAsync(request.ChildId, $"🍪 Съеден перекус\n{request.SnackName}: {request.BreadUnits:0.##} ХЕ\nГлюкоза: {request.CurrentGlucose:0.0} ммоль/л", cancellationToken);

    public Task<NotificationResponse> SendCriticalAlertAsync(CriticalAlertRequest request, CancellationToken cancellationToken = default)
    {
        var location = request.Latitude.HasValue && request.Longitude.HasValue
            ? $"\nКоординаты: {request.Latitude.Value:0.######}, {request.Longitude.Value:0.######}\nКарта: https://yandex.ru/maps/?pt={request.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{request.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&z=16&l=map"
            : string.Empty;
        return SendToParentsAsync(request.ChildId, $"🚨 КРИТИЧЕСКИЙ УРОВЕНЬ ГЛЮКОЗЫ\n{request.CriticalGlucose:0.0} ммоль/л\nВремя: {request.MeasurementTime.ToLocalTime():dd.MM HH:mm}{location}", cancellationToken);
    }

    public async Task SendDailySummaryAsync(long maxUserId, string message, CancellationToken cancellationToken = default) =>
        await SendTextAsync(maxUserId, message, cancellationToken);

    public async Task SendTextAsync(long maxUserId, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new InvalidOperationException("MAX Bot Token не настроен в конфигурации.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"messages?user_id={maxUserId}")
        {
            Content = JsonContent.Create(new { text = message[..Math.Min(message.Length, 4000)], format = "markdown" })
        };
        request.Headers.TryAddWithoutValidation("Authorization", Token);
        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"MAX API error: {(int)response.StatusCode} {error}");
        }
    }

    public async Task RegisterWebhookAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("MAX webhook is not registered: token, URL or secret is missing.");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "subscriptions")
        {
            Content = JsonContent.Create(new { url = WebhookUrl, update_types = new[] { "bot_started", "message_created" }, secret = WebhookSecret })
        };
        request.Headers.TryAddWithoutValidation("Authorization", Token);
        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("MAX webhook registered at {WebhookUrl}.", WebhookUrl);
    }

    private async Task<NotificationResponse> SendToParentsAsync(string childId, string message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(childId, out var childGuid))
        {
            return new NotificationResponse { Success = false, ErrorMessage = "Invalid child id" };
        }

        var recipients = await _db.ParentChildLinks
            .Where(link => link.ChildId == childGuid)
            .Join(_db.Users, link => link.ParentUserId, user => user.UserId, (_, user) => user.MaxUserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        var errors = new List<string>();
        foreach (var recipient in recipients)
        {
            try { await SendTextAsync(recipient, message, cancellationToken); delivered++; }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "MAX notification delivery failed. MaxUserId={MaxUserId}", recipient);
                errors.Add(exception.Message);
            }
        }

        return new NotificationResponse
        {
            Success = delivered > 0,
            ParentsNotified = delivered,
            ErrorMessage = errors.Count == 0 ? null : string.Join("; ", errors)
        };
    }
}
