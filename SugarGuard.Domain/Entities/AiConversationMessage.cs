using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Сообщение внутри AI-конверсации ребёнка.
/// </summary>
[Table("ai_conversation_messages")]
public class AiConversationMessage
{
    /// <summary>
    /// Идентификатор сообщения.
    /// </summary>
    [Key]
    [Column("message_id")]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Идентификатор конверсации.
    /// </summary>
    [Column("conversation_id")]
    [Required]
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Роль сообщения.
    /// </summary>
    [Column("role")]
    public AiMessageRole Role { get; set; }

    /// <summary>
    /// Текст сообщения без секретов и технических токенов.
    /// </summary>
    [Column("text")]
    [MaxLength(4000)]
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор пользователя-автора, если автором был пользователь системы.
    /// </summary>
    [Column("author_user_id")]
    public Guid? AuthorUserId { get; set; }

    /// <summary>
    /// UTC-время создания сообщения.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Название модели, использованной для ответа.
    /// </summary>
    [Column("model")]
    [MaxLength(80)]
    public string? Model { get; set; }

    /// <summary>
    /// Число входных токенов, если провайдер вернул usage.
    /// </summary>
    [Column("input_tokens")]
    public int? InputTokens { get; set; }

    /// <summary>
    /// Число выходных токенов, если провайдер вернул usage.
    /// </summary>
    [Column("output_tokens")]
    public int? OutputTokens { get; set; }

    /// <summary>
    /// Результат проверки безопасности.
    /// </summary>
    [Column("safety_result")]
    public AiSafetyResult SafetyResult { get; set; } = AiSafetyResult.Allowed;

    /// <summary>
    /// Связанная сохранённая рекомендация, если она создана.
    /// </summary>
    [Column("recommendation_id")]
    public Guid? RecommendationId { get; set; }

    /// <summary>
    /// Связанное измерение, если запрос начался с измерения.
    /// </summary>
    [Column("measurement_id")]
    public Guid? MeasurementId { get; set; }

    /// <summary>
    /// Конверсация, которой принадлежит сообщение.
    /// </summary>
    [ForeignKey(nameof(ConversationId))]
    public virtual AiConversation Conversation { get; set; } = null!;

    /// <summary>
    /// Пользователь-автор сообщения.
    /// </summary>
    [ForeignKey(nameof(AuthorUserId))]
    public virtual User? AuthorUser { get; set; }

    /// <summary>
    /// Рекомендация, связанная с ответом.
    /// </summary>
    [ForeignKey(nameof(RecommendationId))]
    public virtual AIRecommendation? Recommendation { get; set; }

    /// <summary>
    /// Измерение, связанное с сообщением.
    /// </summary>
    [ForeignKey(nameof(MeasurementId))]
    public virtual Measurement? Measurement { get; set; }
}
