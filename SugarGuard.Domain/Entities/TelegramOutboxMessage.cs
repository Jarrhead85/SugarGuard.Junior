using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Долговечная очередь сообщений, которые Telegram-бот отправляет через доступный ему канал связи.
/// </summary>
[Table("telegram_outbox_messages")]
public sealed class TelegramOutboxMessage
{
    [Key]
    [Column("telegram_outbox_message_id")]
    public Guid TelegramOutboxMessageId { get; set; } = Guid.NewGuid();

    [Column("telegram_user_id")]
    public long TelegramUserId { get; set; }

    [Column("message_type")]
    [MaxLength(40)]
    public string MessageType { get; set; } = string.Empty;

    [Column("text")]
    [MaxLength(4096)]
    public string Text { get; set; } = string.Empty;

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("requires_acknowledgement")]
    public bool RequiresAcknowledgement { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("next_attempt_at")]
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [Column("delivery_attempts")]
    public int DeliveryAttempts { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("text_delivered_at")]
    public DateTime? TextDeliveredAt { get; set; }

    [Column("location_delivered_at")]
    public DateTime? LocationDeliveredAt { get; set; }

    [Column("acknowledged_at")]
    public DateTime? AcknowledgedAt { get; set; }

    [Column("acknowledged_by_telegram_user_id")]
    public long? AcknowledgedByTelegramUserId { get; set; }

    /// <summary>
    /// Время окончательной неудачи доставки. Такие сообщения больше не
    /// забираются ботом и доступны для диагностики администратору.
    /// </summary>
    [Column("failed_at")]
    public DateTime? FailedAt { get; set; }

    [Column("last_error")]
    [MaxLength(1000)]
    public string? LastError { get; set; }
}
