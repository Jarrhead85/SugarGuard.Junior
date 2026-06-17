namespace SugarGuard.API.DTOs;

/// <summary>
/// Ответ на запрос потребления перекуса
/// </summary>
public sealed class ConsumeSnackResponse
{   
    public string Message { get; init; } = string.Empty; // Сообщение об успехе
   
    public string SnackName { get; init; } = string.Empty; // Название съеденного перекуса
   
    public decimal BreadUnits { get; init; } // Количество хлебных единиц
   
    public DateTime ConsumedAt { get; init; } // Время потребления

    public bool NotificationFailed { get; init; }   

    public string? NotificationError { get; init; } // Краткое описание причины неудачи уведомления
}
