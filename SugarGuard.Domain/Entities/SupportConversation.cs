using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

[Table("support_conversations")]
public sealed class SupportConversation
{
    [Key]
    [Column("conversation_id")]
    public Guid ConversationId { get; set; } = Guid.NewGuid();

    [Column("requester_user_id")]
    public Guid RequesterUserId { get; set; }

    [Required]
    [MaxLength(180)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("status")]
    public SupportConversationStatus Status { get; set; } = SupportConversationStatus.WaitingForSupport;

    [MaxLength(254)]
    [Column("callback_email")]
    public string? CallbackEmail { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    public User RequesterUser { get; set; } = null!;
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
