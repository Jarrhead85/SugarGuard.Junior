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
    Task<GigaChatResponse> GetRecommendationAsync(GigaChatRequest request);

    /// <summary>
    /// Получить access token для GigaChat API
    /// </summary>
    Task<string?> GetAccessTokenAsync();
}

/// <summary>
/// Запрос к GigaChat API
/// </summary>
public class GigaChatRequest
{
    public Guid ChildId { get; set; } // ID ребёнка

    public int ChildAge { get; set; } // Возраст ребёнка

    public string DiabetesType { get; set; } = string.Empty; // Тип диабета ("1 типа" или "2 типа")

    public double CurrentGlucose { get; set; } // Текущий уровень глюкозы в ммоль/л

    public string GlucoseStatus { get; set; } = string.Empty; // Статус глюкозы ("НИЗКО", "НОРМА", "ВЫСОКО", "КРИТИЧЕСКИ")

    public List<double> RecentGlucoseValues { get; set; } = new(); // Последние значения глюкозы за 3 часа

    public string Trend { get; set; } = string.Empty; // Тренд изменения глюкозы ("вверх", "вниз", "стабильно")

    public double TargetRangeMin { get; set; } // Минимальное значение целевого диапазона

    public double TargetRangeMax { get; set; } // Максимальное значение целевого диапазона

    public string InsulinScheme { get; set; } = string.Empty; // Схема инсулинотерапии

    public double InsulinSensitivity { get; set; } // Чувствительность к инсулину (на сколько ммоль/л снижает 1 ед)

    public List<string> AvailableSnacks { get; set; } = new(); // Список доступных перекусов
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
}
