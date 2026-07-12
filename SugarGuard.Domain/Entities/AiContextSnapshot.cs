using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Точный снимок структурированного контекста, отправленного AI.
/// </summary>
[Table("ai_context_snapshots")]
public class AiContextSnapshot
{
    /// <summary>
    /// Идентификатор снимка.
    /// </summary>
    [Key]
    [Column("context_snapshot_id")]
    public Guid ContextSnapshotId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Идентификатор ребёнка.
    /// </summary>
    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; }

    /// <summary>
    /// Идентификатор AI-конверсации.
    /// </summary>
    [Column("conversation_id")]
    [Required]
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Измерение, с которого начался запрос, если оно есть.
    /// </summary>
    [Column("measurement_id")]
    public Guid? MeasurementId { get; set; }

    /// <summary>
    /// Версия формата JSON-контекста.
    /// </summary>
    [Column("format_version")]
    [MaxLength(24)]
    public string FormatVersion { get; set; } = "ai-context-v1";

    /// <summary>
    /// JSON только с фактически переданным AI контекстом.
    /// </summary>
    [Column("context_json")]
    [Required]
    public string ContextJson { get; set; } = "{}";

    /// <summary>
    /// UTC-время создания снимка.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Ребёнок, которому принадлежит снимок.
    /// </summary>
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;

    /// <summary>
    /// Конверсация, к которой относится снимок.
    /// </summary>
    [ForeignKey(nameof(ConversationId))]
    public virtual AiConversation Conversation { get; set; } = null!;

    /// <summary>
    /// Измерение, связанное со снимком.
    /// </summary>
    [ForeignKey(nameof(MeasurementId))]
    public virtual Measurement? Measurement { get; set; }
}
