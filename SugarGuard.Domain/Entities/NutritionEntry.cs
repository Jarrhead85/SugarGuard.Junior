using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

[Table("nutrition_entries")]
public sealed class NutritionEntry
{
    [Key]
    [Column("nutrition_entry_id")]
    public Guid NutritionEntryId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    public Guid ChildId { get; set; }

    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; }

    [Column("meal_type")]
    public MealType MealType { get; set; }

    [Column("meal_name")]
    [MaxLength(120)]
    public string MealName { get; set; } = string.Empty;

    [Column("bread_units", TypeName = "decimal(5,2)")]
    public decimal BreadUnits { get; set; }

    [Column("insulin_units", TypeName = "decimal(5,2)")]
    public decimal InsulinUnits { get; set; }

    [Column("glucose_before", TypeName = "decimal(4,1)")]
    public decimal? GlucoseBefore { get; set; }

    [Column("notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column("source")]
    public NutritionEntrySource Source { get; set; }

    [Column("created_by_user_id")]
    public Guid CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ChildId))]
    public Child Child { get; set; } = null!;
}
