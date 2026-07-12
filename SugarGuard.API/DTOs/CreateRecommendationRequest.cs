using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на создание рекомендации от GigaChat
/// </summary>
public class CreateRecommendationRequest
{
    [Required]
    public Guid ChildId { get; set; } // ID ребёнка для которого запрашивается рекомендация

    public Guid? MeasurementId { get; set; } // ID измерения, для которого запрашивается рекомендация

    [Required]
    [Range(1.0, 30.0, ErrorMessage = "Уровень глюкозы должен быть в диапазоне 1.0-30.0 ммоль/л")]
    public decimal GlucoseValue { get; set; } // Текущий уровень глюкозы в ммоль/л

    public List<string>? AvailableSnacks { get; set; } // Список доступных перекусов в рюкзаке

    public bool ForceNew { get; set; } = false; // Принудительно запросить новую рекомендацию

    /// <summary>
    /// Текст вопроса пользователя для AI-консультанта.
    /// </summary>
    [MaxLength(600)]
    public string? Question { get; set; }

    /// <summary>
    /// Идентификатор активной AI-конверсации, если клиент её продолжает.
    /// </summary>
    public Guid? ConversationId { get; set; }
}
