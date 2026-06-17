using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Рекомендация от ИИ
/// </summary>
[Table("ai_recommendations")]
public class AIRecommendation
{
    [Key]
    [Column("recommendation_id")]
    public Guid RecommendationId { get; set; } = Guid.NewGuid(); // Уникальный идентификатор рекомендации

    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; } // Идентификатор ребенка, для которого направлена рекомендация

    [Column("measurement_id")]
    public Guid? MeasurementId { get; set; } // Идентификатор измерения глюкозы, на основе которого сделана рекомендация

    [Column("glucose_value_at_request", TypeName = "decimal(4,1)")] // Значение уровня глюкозы на момент запроса рекомендации
    [Required]
    public decimal GlucoseValueAtRequest { get; set; }

    [Column("recommendation_text")]
    [Required]
    public string RecommendationText { get; set; } = string.Empty; // Текст рекомендации, сформулированный ИИ

    [Column("urgency")]
    [MaxLength(50)]
    public string? Urgency { get; set; } // Уровень неотложности рекомендации

    [Column("model_used")]
    [MaxLength(100)]
    public string? ModelUsed { get; set; } // Имя используемой модели ИИ

    [Column("is_from_cache")]
    public bool IsFromCache { get; set; } = false; // Флаг, указывающий на то, является ли рекомендация из кеша

    [Column("latency_ms")]
    public int? LatencyMs { get; set; } // Время задержки в миллисекундах при генерации рекомендации

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Время создания рекомендации

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!; // Связь с записью ребенка в базе данных

    [ForeignKey(nameof(MeasurementId))]
    public virtual Measurement? Measurement { get; set; } // Связь с измерением глюкозы, если оно найдено
}
