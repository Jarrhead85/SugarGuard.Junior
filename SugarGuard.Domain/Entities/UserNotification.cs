using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Persistent notification addressed to a specific SugarGuard user.
/// </summary>
[Table("user_notifications")]
public sealed class UserNotification
{
    [Key]
    [Column("notification_id")]
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    [Column("recipient_user_id")]
    public Guid RecipientUserId { get; set; }

    [Column("child_id")]
    public Guid? ChildId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("type")]
    public string Type { get; set; } = "info";

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("source_type")]
    public string SourceType { get; set; } = string.Empty;

    [Column("source_id")]
    public Guid SourceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_read")]
    public bool IsRead { get; set; }

    public User RecipientUser { get; set; } = null!;
    public Child? Child { get; set; }
}
