using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("audit_log_id")]
    public Guid AuditLogId { get; set; } = Guid.NewGuid();

    [Column("actor_user_id")]
    public Guid? ActorUserId { get; set; }

    [Column("action")]
    [MaxLength(128)]
    public string Action { get; set; } = string.Empty;

    [Column("target_type")]
    [MaxLength(128)]
    public string? TargetType { get; set; }

    [Column("target_id")]
    [MaxLength(128)]
    public string? TargetId { get; set; }

    [Column("details")]
    [MaxLength(4000)]
    public string? Details { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
