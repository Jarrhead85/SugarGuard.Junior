using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

[Table("child_achievements")]
public sealed class ChildAchievement
{
    [Key]
    [Column("child_achievement_id")]
    public Guid ChildAchievementId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    public Guid ChildId { get; set; }

    [Column("achievement_code")]
    [MaxLength(64)]
    public string AchievementCode { get; set; } = string.Empty;

    [Column("unlocked_at")]
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ChildId))]
    public Child Child { get; set; } = null!;
}
