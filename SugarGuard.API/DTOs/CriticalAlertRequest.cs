namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отправку критического уведомления с геолокацией
/// </summary>
public class CriticalAlertRequest
{
    public string ChildId { get; set; } = string.Empty; // ID ребёнка

    public double CriticalGlucose { get; set; } // Критическое значение глюкозы в ммоль/л

    public DateTime MeasurementTime { get; set; } // Время измерения

    public double? Latitude { get; set; } // Широта

    public double? Longitude { get; set; } // Долгота

    public string? Address { get; set; } // Адрес местоположения
}
