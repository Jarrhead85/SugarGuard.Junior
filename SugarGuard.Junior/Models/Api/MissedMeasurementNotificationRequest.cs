// DTO для уведомления о пропущенном измерении
namespace SugarGuard.Junior.Models.Api;

/// <summary>
/// Запрос на отправку уведомления родителям о пропущенном измерении
/// </summary>
public class MissedMeasurementNotificationRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Запланированное время измерения
    /// </summary>
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Время когда измерение было пропущено
    /// </summary>
    public DateTime MissedAt { get; set; }

    /// <summary>
    /// Количество минут опоздания
    /// </summary>
    public int MinutesLate { get; set; }

    /// <summary>
    /// Номер повторного напоминания (1, 2, 3...)
    /// </summary>
    public int ReminderNumber { get; set; }
}