using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Запись лога синхронизации между MAUI-клиентом и сервером
/// </summary>
[Table("sync_logs")]
public class SyncLog
{   
    public const string ResolutionSourceServerAuto = "server_auto"; // Конфликт разрешён автоматически
   
    public const string ResolutionSourceManual = "manual"; // Конфликт разрешён вручную

    [Key]
    [Column("sync_log_id")]
    public Guid SyncLogId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    public Guid ChildId { get; set; }

    [Column("entity_type")]
    [MaxLength(128)]
    public string EntityType { get; set; } = string.Empty;

    [Column("entity_id")]
    [MaxLength(128)]
    public string EntityId { get; set; } = string.Empty;

    [Column("status")]
    [MaxLength(32)]
    public string Status { get; set; } = "success";

    [Column("error")]
    [MaxLength(4000)]
    public string? Error { get; set; }

    [Column("is_conflict")]
    public bool IsConflict { get; set; }

    [Column("resolution_source")]
    [MaxLength(32)]
    public string? ResolutionSource { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
