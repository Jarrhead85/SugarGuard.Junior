namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отправку уведомления о съеденном перекусе родителям
/// </summary>
public class SnackConsumedNotificationRequest
{
    public string ChildId { get; set; } = string.Empty; // ID ребёнка

    public string SnackName { get; set; } = string.Empty; // Название перекуса

    public decimal BreadUnits { get; set; } // Количество хлебных единиц

    public double CurrentGlucose { get; set; } // Текущий уровень глюкозы в ммоль/л

    public DateTime ConsumedAt { get; set; } // Время употребления перекуса
}
