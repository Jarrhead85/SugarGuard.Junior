using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>Работа с официальным Bot API мессенджера MAX.</summary>
public interface IMaxBotService
{
    bool IsConfigured { get; }
    string? PublicBotUrl { get; }

    Task<NotificationResponse> SendMeasurementNotificationAsync(MeasurementNotificationRequest request, CancellationToken cancellationToken = default);
    Task<NotificationResponse> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request, CancellationToken cancellationToken = default);
    Task<NotificationResponse> SendCriticalAlertAsync(CriticalAlertRequest request, CancellationToken cancellationToken = default);
    Task SendDailySummaryAsync(long maxUserId, string message, CancellationToken cancellationToken = default);
    Task SendTextAsync(long maxUserId, string message, CancellationToken cancellationToken = default);
    Task RegisterWebhookAsync(CancellationToken cancellationToken = default);
}
