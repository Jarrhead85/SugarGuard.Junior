namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отправку уведомления об измерении глюкозы родителям
/// </summary>
public class MeasurementNotificationRequest
{
    public string ChildId { get; set; } = string.Empty; // ID ребёнка

    public double GlucoseValue { get; set; } // Значение глюкозы

    public string Status { get; set; } = string.Empty; // Статус глюкозы

    public DateTime MeasurementTime { get; set; } // Время измерения

    public string? Notes { get; set; } // Дополнительные заметки
}
