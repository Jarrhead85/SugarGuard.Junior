using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для создания нового измерения глюкозы
/// </summary>
public class CreateMeasurementRequest
{
    /// <summary>
    /// Optional client-generated identifier. Mobile clients use it to make sync idempotent.
    /// </summary>
    public Guid? MeasurementId { get; set; }

    [Required]
    public Guid ChildId { get; set; }

    [Required]
    [Range(1.0, 30.0, ErrorMessage = "Уровень глюкозы должен быть в диапазоне 1.0-30.0 ммоль/л")]
    public decimal GlucoseValue { get; set; }

    [Required]
    public DateTime MeasurementTime { get; set; }

    [MaxLength(50)]
    public string? ChildState { get; set; }

    public string? Notes { get; set; }

    [MaxLength(50)]
    public string? DataSource { get; set; } = "mobile_app";

    /// <summary>
    /// Широта устройства в момент критического измерения, если мобильное приложение смогло её получить.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Долгота устройства в момент критического измерения, если мобильное приложение смогло её получить.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Адрес местоположения, если мобильное приложение смогло его определить.
    /// </summary>
    [MaxLength(512)]
    public string? Address { get; set; }
}
