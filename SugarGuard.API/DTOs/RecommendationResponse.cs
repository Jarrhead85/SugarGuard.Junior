namespace SugarGuard.API.DTOs;

/// <summary>
/// Ответ с рекомендацией от ИИ
/// </summary>
public class RecommendationResponse
{
    public Guid RecommendationId { get; set; } // Уникальный ID рекомендации

    public Guid ChildId { get; set; } // ID ребёнка   

    public Guid? MeasurementId { get; set; } // ID связанного измерения   

    public decimal GlucoseValueAtRequest { get; set; } // Уровень глюкозы на момент запроса   

    public string RecommendationText { get; set; } = string.Empty; // Текст рекомендации от ИИ   

    public string? Urgency { get; set; } // Уровень срочности рекомендации   

    public string? ModelUsed { get; set; } // Модель ИИ, использованная для генерации   

    public bool IsFromCache { get; set; } // Была ли рекомендация взята из кэша     

    public int? LatencyMs { get; set; } // Время отклика в миллисекундах   

    public DateTime CreatedAt { get; set; } // Время создания рекомендации
}

/// <summary>
/// Страница истории рекомендаций
/// </summary>
public class RecommendationHistoryPage
{
    public IReadOnlyList<RecommendationResponse> Items { get; init; } // Записи текущей страницы
        = Array.Empty<RecommendationResponse>();

    public DateTime? NextCursor { get; init; } // Курсор для следующей страницы

    public bool HasMore => NextCursor.HasValue; // Имеется ли следующая страница
}
