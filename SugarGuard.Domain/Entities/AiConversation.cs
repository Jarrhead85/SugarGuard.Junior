using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Долгосрочная AI-конверсация, принадлежащая медицинской истории ребёнка.
/// </summary>
[Table("ai_conversations")]
public class AiConversation
{
    /// <summary>
    /// Идентификатор конверсации.
    /// </summary>
    [Key]
    [Column("conversation_id")]
    public Guid ConversationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Идентификатор ребёнка, которому принадлежит память.
    /// </summary>
    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; }

    /// <summary>
    /// Текущий статус конверсации.
    /// </summary>
    [Column("status")]
    public AiConversationStatus Status { get; set; } = AiConversationStatus.Active;

    /// <summary>
    /// Краткое безопасное резюме последних разговоров.
    /// </summary>
    [Column("summary")]
    [MaxLength(2000)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// UTC-время последнего обновления резюме.
    /// </summary>
    [Column("summary_updated_at")]
    public DateTime? SummaryUpdatedAt { get; set; }

    /// <summary>
    /// UTC-время последнего сообщения.
    /// </summary>
    [Column("last_message_at")]
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// UTC-время создания конверсации.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Технический идентификатор беседы у провайдера, если он используется.
    /// </summary>
    [Column("provider_conversation_id")]
    [MaxLength(128)]
    public string? ProviderConversationId { get; set; }

    /// <summary>
    /// Ребёнок, которому принадлежит конверсация.
    /// </summary>
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;

    /// <summary>
    /// Сообщения конверсации.
    /// </summary>
    public virtual ICollection<AiConversationMessage> Messages { get; set; } = new List<AiConversationMessage>();

    /// <summary>
    /// Снимки контекста, отправленного AI.
    /// </summary>
    public virtual ICollection<AiContextSnapshot> ContextSnapshots { get; set; } = new List<AiContextSnapshot>();
}
