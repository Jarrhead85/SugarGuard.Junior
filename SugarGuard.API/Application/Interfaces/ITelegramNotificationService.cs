using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Интерфейс для отправки уведомлений родителям через Telegram
/// </summary>
public interface ITelegramNotificationService
{
    /// <summary>
    /// Отправляет уведомление об измерении глюкозы всем родителям ребёнка
    /// </summary>
    Task<NotificationResponse> SendMeasurementNotificationAsync(MeasurementNotificationRequest request);

    /// <summary>
    /// Отправляет уведомление о съеденном перекусе всем родителям ребёнка
    /// </summary>
    Task<NotificationResponse> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request);

    /// <summary>
    /// Отправляет критическое уведомление с геолокацией всем родителям ребёнка
    /// </summary>
    Task<NotificationResponse> SendCriticalAlertAsync(CriticalAlertRequest request);
}
