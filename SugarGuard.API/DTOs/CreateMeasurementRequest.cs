using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для создания нового измерения глюкозы
/// </summary>
public class CreateMeasurementRequest
{
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
}
