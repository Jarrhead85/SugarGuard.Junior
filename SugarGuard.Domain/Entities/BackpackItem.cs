using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Активный перекус в рюкзаке ребёнка
/// </summary>
[Table("backpack_items")]
public class BackpackItem
{
    [Key]
    [Column("backpack_item_id")]
    public Guid BackpackItemId { get; set; } = Guid.NewGuid();

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

    [Column("added_by")]
    [MaxLength(50)]
    public string? AddedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;
}
