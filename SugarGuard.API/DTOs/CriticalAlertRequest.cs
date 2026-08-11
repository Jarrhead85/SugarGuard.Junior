namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отправку критического уведомления с геолокацией
/// </summary>
public class CriticalAlertRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(36, MinimumLength = 36)]
    public string ChildId { get; set; } = string.Empty; // ID ребёнка

    [System.ComponentModel.DataAnnotations.Range(0.5, 50.0)]
    public double CriticalGlucose { get; set; } // Критическое значение глюкозы в ммоль/л

    public DateTime MeasurementTime { get; set; } // Время измерения

    [System.ComponentModel.DataAnnotations.Range(-90.0, 90.0)]
    public double? Latitude { get; set; } // Широта

    [System.ComponentModel.DataAnnotations.Range(-180.0, 180.0)]
    public double? Longitude { get; set; } // Долгота

    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? Address { get; set; } // Адрес местоположения

    /// <summary>Признак ручного SOS-вызова ребёнка, а не автоматического алерта.</summary>
    public bool IsEmergencyHelp { get; set; }
}
