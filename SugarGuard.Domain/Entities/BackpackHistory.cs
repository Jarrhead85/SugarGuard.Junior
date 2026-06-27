using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// История изменений рюкзака
/// </summary>
[Table("backpack_history")]
public class BackpackHistory
{
    [Key]
    [Column("history_id")]
    public Guid HistoryId { get; set; } = Guid.NewGuid(); // Уникальный идентификатор записи в истории изменений рюкзака

    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; } // Идентификатор ребенка, для которого ведется история рюкзака

    [Column("snack_name")]
    [MaxLength(500)]
    [Required]
    public string SnackName { get; set; } = string.Empty; // Название перекуса

    [Column("bread_units", TypeName = "decimal(4,2)")]
    [Required]
    public decimal BreadUnits { get; set; } // Количество хлебных единиц в перекусе

    [Column("added_at")]
    public DateTime? AddedAt { get; set; } // Дата и время добавления перекуса в рюкзак

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; } // Дата и время удаления перекуса из рюкзака

    [Column("deleted_by")]
    [MaxLength(80)]
    public string? DeletedBy { get; set; } // Идентификатор пользователя, который удалил перекус

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Дата и время создания записи в истории

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!; // Ссылка на ребенка, чей рюкзак был изменен
}
