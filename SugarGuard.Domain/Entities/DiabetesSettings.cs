using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Настройки диабета для конкретного ребёнка
/// </summary>
[Table("diabetes_settings")]
public class DiabetesSettings
{
    [Key]
    [Column("child_id")]
    public Guid ChildId { get; set; }

    [Column("target_range_min", TypeName = "decimal(4,1)")]
    public decimal TargetRangeMin { get; set; } = 4.0m;

    [Column("target_range_max", TypeName = "decimal(4,1)")]
    public decimal TargetRangeMax { get; set; } = 10.0m;

    [Column("insulin_sensitivity", TypeName = "decimal(4,2)")]
    public decimal InsulinSensitivity { get; set; } = 1.5m;

    [Column("carb_insulin_ratio", TypeName = "decimal(5,2)")]
    public decimal CarbInsulinRatio { get; set; } = 10.0m;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;
}
