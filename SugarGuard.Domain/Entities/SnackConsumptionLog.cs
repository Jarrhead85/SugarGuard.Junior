using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Лог съеденных перекусов
/// </summary>
[Table("snack_consumption_logs")]
public class SnackConsumptionLog
{
    [Key]
    [Column("log_id")]
    public Guid LogId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; }

    [Column("snack_name")]
    [MaxLength(500)]
    [Required]
    public string SnackName { get; set; } = string.Empty;

    [Column("bread_units", TypeName = "decimal(4,2)")]
    [Required]
    public decimal BreadUnits { get; set; }

    [Column("recommendation_id")]
    public Guid? RecommendationId { get; set; }

    [Column("consumed_at")]
    [Required]
    public DateTime ConsumedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;

    [ForeignKey(nameof(RecommendationId))]
    public virtual AIRecommendation? AIRecommendation { get; set; }
}
