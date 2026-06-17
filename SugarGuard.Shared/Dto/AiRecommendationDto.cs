namespace SugarGuard.Shared.Dto;

/// <summary>
/// DTO AI-рекомендации для Blazor-компонента
/// </summary>
public sealed class AiRecommendationDto
{
    public string RecommendationText { get; init; } = string.Empty;

    public string? Urgency { get; init; }

    public string? ModelUsed { get; init; }
   
    public bool IsFromCache { get; init; } // true — ответ взят из кэша похожей ситуации
   
    public long LatencyMs { get; init; } // Время генерации ответа GigaChat в миллисекундах 
   
    public DateTime CreatedAt { get; init; } // время создания рекомендации
}
