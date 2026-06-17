using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Временный код для привязки родителя к ребёнку
/// </summary>
[Table("connection_codes")]
public class ConnectionCode
{
    [Key]
    [Column("code_id")]
    public Guid CodeId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; }

    [Column("code_hash")]
    [MaxLength(64)]
    [Required]
    public string CodeHash { get; set; } = string.Empty;

    [Column("expires_at")]
    [Required]
    public DateTime ExpiresAt { get; set; }

    [Column("is_used")]
    public bool IsUsed { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;
}
