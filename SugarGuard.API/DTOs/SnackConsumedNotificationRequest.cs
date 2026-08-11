namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отправку уведомления о съеденном перекусе родителям
/// </summary>
public class SnackConsumedNotificationRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(36, MinimumLength = 36)]
    public string ChildId { get; set; } = string.Empty; // ID ребёнка

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string SnackName { get; set; } = string.Empty; // Название перекуса

    [System.ComponentModel.DataAnnotations.Range(0.0, 20.0)]
    public decimal BreadUnits { get; set; } // Количество хлебных единиц

    [System.ComponentModel.DataAnnotations.Range(0.5, 50.0)]
    public double CurrentGlucose { get; set; } // Текущий уровень глюкозы в ммоль/л

    public DateTime ConsumedAt { get; set; } // Время употребления перекуса
}
