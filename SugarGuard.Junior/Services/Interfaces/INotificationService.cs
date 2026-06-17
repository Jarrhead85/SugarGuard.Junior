// Интерфейс для отправки уведомлений
namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Интерфейс для управления уведомлениями и напоминаниями
/// </summary>
public interface INotificationService : IDisposable
{
    /// <summary>
    /// Отправляет локальное уведомление на устройство
    /// </summary>
    Task<bool> SendLocalNotificationAsync(string title, string message, string notificationId);

    /// <summary>
    /// Отправляет SMS родителю в критической ситуации
    /// </summary>
    Task<bool> SendSMSAsync(string phoneNumber, string message);

    /// <summary>
    /// Запланирует уведомление на определённое время
    /// </summary>
    Task<bool> ScheduleNotificationAsync(string title, string message, string notificationId, DateTime scheduledTime);

    /// <summary>
    /// Отменяет запланированное уведомление
    /// </summary>
    Task<bool> CancelNotificationAsync(string notificationId);

    /// <summary>
    /// Отправляет полноэкранное уведомление при критическом уровне
    /// </summary>
    Task<bool> SendCriticalAlertAsync(string title, string message, double glucoseValue);

    /// <summary>
    /// Планирует все напоминания для ребёнка на основе расписания
    /// </summary>
    Task<bool> ScheduleAllRemindersAsync(string childId);

    /// <summary>
    /// Отменяет все напоминания для ребёнка
    /// </summary>
    Task<bool> CancelAllRemindersAsync(string childId);

    /// <summary>
    /// Отмечает измерение как выполненное (останавливает повторные напоминания)
    /// </summary>
    Task<bool> MarkMeasurementCompletedAsync(string childId);
}
