namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отправку уведомления об измерении глюкозы родителям
/// </summary>
public class MeasurementNotificationRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(36, MinimumLength = 36)]
    public string ChildId { get; set; } = string.Empty; // ID ребёнка

    [System.ComponentModel.DataAnnotations.Range(0.5, 50.0)]
    public double GlucoseValue { get; set; } // Значение глюкозы

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string Status { get; set; } = string.Empty; // Статус глюкозы

    public DateTime MeasurementTime { get; set; } // Время измерения

    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? Notes { get; set; } // Дополнительные заметки
}
