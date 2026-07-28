using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Последний сигнал работоспособности внешнего сервиса SugarGuard.
/// </summary>
[Table("bot_service_heartbeats")]
public sealed class BotServiceHeartbeat
{
    [Key]
    [Column("bot_name")]
    [MaxLength(64)]
    public string BotName { get; set; } = string.Empty;

    [Column("last_heartbeat_at")]
    public DateTime LastHeartbeatAt { get; set; }

    [Column("internet_available")]
    public bool InternetAvailable { get; set; }

    [Column("last_external_api_success_at")]
    public DateTime? LastExternalApiSuccessAt { get; set; }

    [Column("last_error")]
    [MaxLength(1000)]
    public string? LastError { get; set; }

    [Column("version")]
    [MaxLength(80)]
    public string? Version { get; set; }
}
