using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

[Table("support_messages")]
public sealed class SupportMessage
{
    [Key]
    [Column("message_id")]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    [Column("conversation_id")]
    public Guid ConversationId { get; set; }

    [Column("author_user_id")]
    public Guid AuthorUserId { get; set; }

    [Required]
    [MaxLength(4000)]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("read_by_requester")]
    public bool ReadByRequester { get; set; }

    [Column("read_by_support")]
    public bool ReadBySupport { get; set; }

    public SupportConversation Conversation { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}
