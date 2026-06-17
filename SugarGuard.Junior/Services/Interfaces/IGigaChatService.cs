// Интерфейс для работы с GigaChat API
using SugarGuard.Junior.Models.Api;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Запрос к GigaChat API
/// </summary>
public class GigaChatRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Возраст ребёнка
    /// </summary>
    public int ChildAge { get; set; }

    /// <summary>
    /// Тип диабета ("1 типа" или "2 типа")
    /// </summary>
    public string DiabetesType { get; set; } = string.Empty;

    /// <summary>
    /// Текущий уровень глюкозы
    /// </summary>
    public double CurrentGlucose { get; set; }

    /// <summary>
    /// Статус глюкозы ("НИЗКО", "НОРМА", "ВЫСОКО", "КРИТИЧЕСКИ")
    /// </summary>
    public string GlucoseStatus { get; set; } = string.Empty;

    /// <summary>
    /// Последние значения глюкозы за 3 часа
    /// </summary>
    public List<double> RecentGlucoseValues { get; set; } = [];

    /// <summary>
    /// Тренд глюкозы ("вверх", "вниз", "стабильно")
    /// </summary>
    public string Trend { get; set; } = string.Empty;

    /// <summary>
    /// Минимальное значение целевого диапазона
    /// </summary>
    public double TargetRangeMin { get; set; }

    /// <summary>
    /// Максимальное значение целевого диапазона
    /// </summary>
    public double TargetRangeMax { get; set; }

    /// <summary>
    /// Схема инсулинотерапии
    /// </summary>
    public string InsulinScheme { get; set; } = string.Empty;

    /// <summary>
    /// Чувствительность к инсулину
    /// </summary>
    public double InsulinSensitivity { get; set; }

    /// <summary>
    /// Доступные перекусы в рюкзаке
    /// </summary>
    public List<string> AvailableSnacks { get; set; } = [];
}

/// <summary>
/// Ответ от GigaChat API
/// </summary>
public class GigaChatResponse
{
    /// <summary>
    /// Успешно ли выполнен запрос
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Текст рекомендации от ИИ
    /// </summary>
    public string RecommendationText { get; set; } = string.Empty;

    /// <summary>
    /// Уровень срочности
    /// </summary>
    public string Urgency { get; set; } = "Normal";

    /// <summary>
    /// Время обработки в миллисекундах
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Сообщение об ошибке (если есть)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Использованная модель
    /// </summary>
    public string ModelUsed { get; set; } = "GigaChat";
}

/// <summary>
/// Интерфейс для работы с GigaChat API
/// </summary>
public interface IGigaChatService
{
    /// <summary>
    /// Получает рекомендацию от GigaChat
    /// </summary>
    /// <param name="request">Запрос с данными о состоянии ребёнка</param>
    /// <returns>Рекомендация от ИИ или null при ошибке</returns>
    Task<GigaChatResponse?> GetRecommendationAsync(GigaChatRequest request);

    /// <summary>
    /// Получает access token для работы с GigaChat API
    /// </summary>
    /// <returns>Access token или null при ошибке</returns>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// Проверяет доступность GigaChat API
    /// </summary>
    /// <returns>True если API доступен</returns>
    Task<bool> IsAvailableAsync();
}