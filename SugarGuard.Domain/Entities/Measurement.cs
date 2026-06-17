using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Измерение уровня глюкозы в крови
/// </summary>
[Table("measurements")]
public class Measurement
{
    [Key]
    [Column("measurement_id")]
    public Guid MeasurementId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; }

    [Column("glucose_value", TypeName = "decimal(4,1)")]
    [Required]
    public decimal GlucoseValue { get; set; }

    [Column("measurement_time")]
    [Required]
    public DateTime MeasurementTime { get; set; }

    [Column("child_state")]
    [MaxLength(50)]
    public string? ChildState { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("data_source")]
    [MaxLength(50)]
    public string? DataSource { get; set; }

    [Column("recommendation_id")]
    public Guid? RecommendationId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("glucose_ui_state")]
    [MaxLength(16)]
    public string? GlucoseUiState { get; private set; }

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;

    [ForeignKey(nameof(RecommendationId))]
    public virtual AIRecommendation? AIRecommendation { get; set; }
}
