using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.MaxBot.Abstractions;

namespace SugarGuard.API.Application.Services;

/// <summary>Оркестрирует доставку уведомлений SugarGuard через MAX.</summary>
public sealed class MaxBotService : IMaxBotService
{
    private readonly AppDbContext _db;
    private readonly IMaxBotClient _client;
    private readonly ILogger<MaxBotService> _logger;

    public MaxBotService(AppDbContext db, IMaxBotClient client, ILogger<MaxBotService> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    public bool IsConfigured => _client.IsConfigured;
    public string? PublicBotUrl => _client.PublicBotUrl;

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

    public Task SendDailySummaryAsync(long maxUserId, string message, CancellationToken cancellationToken = default) =>
        _client.SendTextAsync(maxUserId, message, cancellationToken);

    public Task SendTextAsync(long maxUserId, string message, CancellationToken cancellationToken = default) =>
        _client.SendTextAsync(maxUserId, message, cancellationToken);

    public Task RegisterWebhookAsync(CancellationToken cancellationToken = default) =>
        _client.RegisterWebhookAsync(cancellationToken);

    private async Task<NotificationResponse> SendToParentsAsync(string childId, string message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(childId, out var childGuid))
        {
            return new NotificationResponse { Success = false, ErrorMessage = "Некорректный идентификатор ребёнка." };
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
            try
            {
                await _client.SendTextAsync(recipient, message, cancellationToken);
                delivered++;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Не удалось доставить уведомление MAX. MaxUserId={MaxUserId}", recipient);
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
