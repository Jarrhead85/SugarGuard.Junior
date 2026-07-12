namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Интерфейс для работы с GigaChat API
/// Обеспечивает получение ИИ-рекомендаций на основе медицинских данных
/// </summary>
public interface IGigaChatService
{
    /// <summary>
    /// Получить рекомендацию от GigaChat на основе данных ребёнка
    /// </summary>
    Task<GigaChatResponse> GetRecommendationAsync(
        GigaChatRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить access token для GigaChat API
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Запрос к GigaChat API
/// </summary>
public class GigaChatRequest
{
    /// <summary>
    /// ID ребёнка.
    /// </summary>
    public Guid ChildId { get; set; } // ID ребёнка

    /// <summary>
    /// Возраст ребёнка.
    /// </summary>
    public int ChildAge { get; set; } // Возраст ребёнка

    /// <summary>
    /// Тип диабета.
    /// </summary>
    public string DiabetesType { get; set; } = string.Empty; // Тип диабета ("1 типа" или "2 типа")

    /// <summary>
    /// Текущий уровень глюкозы в ммоль/л.
    /// </summary>
    public double CurrentGlucose { get; set; } // Текущий уровень глюкозы в ммоль/л

    /// <summary>
    /// Статус глюкозы.
    /// </summary>
    public string GlucoseStatus { get; set; } = string.Empty; // Статус глюкозы ("НИЗКО", "НОРМА", "ВЫСОКО", "КРИТИЧЕСКИ")

    /// <summary>
    /// Последние значения глюкозы.
    /// </summary>
    public List<double> RecentGlucoseValues { get; set; } = new(); // Последние значения глюкозы за 3 часа

    /// <summary>
    /// Тренд изменения глюкозы.
    /// </summary>
    public string Trend { get; set; } = string.Empty; // Тренд изменения глюкозы ("вверх", "вниз", "стабильно")

    /// <summary>
    /// Минимальное значение целевого диапазона.
    /// </summary>
    public double TargetRangeMin { get; set; } // Минимальное значение целевого диапазона

    /// <summary>
    /// Максимальное значение целевого диапазона.
    /// </summary>
    public double TargetRangeMax { get; set; } // Максимальное значение целевого диапазона

    /// <summary>
    /// Схема инсулинотерапии.
    /// </summary>
    public string InsulinScheme { get; set; } = string.Empty; // Схема инсулинотерапии

    /// <summary>
    /// Чувствительность к инсулину.
    /// </summary>
    public double InsulinSensitivity { get; set; } // Чувствительность к инсулину (на сколько ммоль/л снижает 1 ед)

    /// <summary>
    /// Список доступных перекусов.
    /// </summary>
    public List<string> AvailableSnacks { get; set; } = new(); // Список доступных перекусов

    /// <summary>
    /// Вопрос пользователя.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Структурированный обезличенный контекст для AI.
    /// </summary>
    public string? StructuredContextJson { get; set; }
}

/// <summary>
/// Ответ от GigaChat API
/// </summary>
public class GigaChatResponse
{
    public string RecommendationText { get; set; } = string.Empty; // Текст рекомендации от ИИ

    public string? Urgency { get; set; } // Уровень срочности

    public string ModelUsed { get; set; } = "GigaChat"; // Модель, использованная для генерации

    public int LatencyMs { get; set; } // Время отклика в миллисекундах

    public bool IsLocalFallback { get; set; } = false; // Была ли использована локальная рекомендация

    public bool IsSuccess { get; set; } = true; // Успешность запроса

    public string? ErrorMessage { get; set; } // Сообщение об ошибке (если есть)

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
