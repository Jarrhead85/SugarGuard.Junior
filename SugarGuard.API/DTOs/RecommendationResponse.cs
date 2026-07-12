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

    /// <summary>
    /// Идентификатор AI-конверсации ребёнка.
    /// </summary>
    public Guid? ConversationId { get; set; }

    /// <summary>
    /// Был ли использован локальный безопасный fallback.
    /// </summary>
    public bool IsLocalFallback { get; set; }

    /// <summary>
    /// Результат проверки безопасности.
    /// </summary>
    public string? SafetyResult { get; set; }

    /// <summary>
    /// Число входных токенов, если провайдер вернул usage.
    /// </summary>
    public int? InputTokens { get; set; }

    /// <summary>
    /// Число выходных токенов, если провайдер вернул usage.
    /// </summary>
    public int? OutputTokens { get; set; }

    /// <summary>
    /// Общее число токенов, если провайдер вернул usage.
    /// </summary>
    public int? TotalTokens { get; set; }
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

/// <summary>
/// История пользовательского AI-диалога ребёнка без технических данных.
/// </summary>
public sealed class AiConversationHistoryResponse
{
    /// <summary>
    /// Идентификатор активной конверсации, если она уже создана.
    /// </summary>
    public Guid? ConversationId { get; init; }

    /// <summary>
    /// Краткое резюме диалога.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Последние сообщения диалога.
    /// </summary>
    public IReadOnlyList<AiConversationMessageResponse> Messages { get; init; } = [];
}

/// <summary>
/// Сообщение пользовательской истории AI-диалога.
/// </summary>
public sealed class AiConversationMessageResponse
{
    /// <summary>
    /// Идентификатор сообщения.
    /// </summary>
    public Guid MessageId { get; init; }

    /// <summary>
    /// Роль сообщения: User, Assistant или System.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Текст сообщения.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Время создания сообщения.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Связанное измерение, если запрос начался с него.
    /// </summary>
    public Guid? MeasurementId { get; init; }

    /// <summary>
    /// Связанная рекомендация, если сообщение является ответом AI.
    /// </summary>
    public Guid? RecommendationId { get; init; }

    /// <summary>
    /// Результат проверки безопасности, если применимо.
    /// </summary>
    public string? SafetyResult { get; init; }
}
