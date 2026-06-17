// Модель расписания измерений глюкозы
namespace SugarGuard.Junior.Models.Core;

/// <summary>
/// Элемент расписания измерений для ребёнка
/// Хранит время когда нужно напомнить об измерении
/// </summary>
public class MeasurementSchedule
{
    /// <summary>
    /// Уникальный ID элемента расписания
    /// </summary>
    public string ScheduleId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID ребёнка, к которому относится расписание
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Время измерения (только время, без даты)
    /// Например: 08:00, 12:30, 18:00
    /// </summary>
    public TimeOnly ScheduledTime { get; set; }

    /// <summary>
    /// Активно ли это время (можно временно отключить)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Синхронизировано ли с сервером
    /// </summary>
    public bool IsSynced { get; set; } = false;

    /// <summary>
    /// Форматированное время для отображения
    /// </summary>
    public string FormattedTime => ScheduledTime.ToString("HH:mm");

    /// <summary>
    /// Следующее время измерения (сегодня или завтра)
    /// </summary>
    public DateTime GetNextScheduledDateTime()
    {
        var today = DateTime.Today;
        var scheduledDateTime = today.Add(ScheduledTime.ToTimeSpan());
        
        // Если время уже прошло сегодня, берём завтра
        if (scheduledDateTime <= DateTime.Now)
        {
            scheduledDateTime = scheduledDateTime.AddDays(1);
        }
        
        return scheduledDateTime;
    }
}