// Интерфейс для оркестратора рекомендаций
namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Запрос на получение рекомендации для оркестратора
/// </summary>
public class OrchestratorRecommendationRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Текущее значение глюкозы
    /// </summary>
    public double GlucoseValue { get; set; }

    /// <summary>
    /// Последние значения глюкозы
    /// </summary>
    public List<double> RecentGlucoseValues { get; set; } = [];

    /// <summary>
    /// Состояние ребёнка
    /// </summary>
    public string ChildState { get; set; } = string.Empty;

    /// <summary>
    /// Доступные перекусы
    /// </summary>
    public List<string> AvailableSnacks { get; set; } = [];
}

/// <summary>
/// Результат получения рекомендации
/// </summary>
public class RecommendationResult
{
    /// <summary>
    /// Текст рекомендации
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Источник рекомендации
    /// </summary>
    public RecommendationSource Source { get; set; }

    /// <summary>
    /// Рекомендация из кэша?
    /// </summary>
    public bool IsFromCache { get; set; }

    /// <summary>
    /// Дополнительное сообщение (например, об ошибке)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Время обработки в миллисекундах
    /// </summary>
    public long LatencyMs { get; set; }
}

/// <summary>
/// Источник рекомендации
/// </summary>
public enum RecommendationSource
{
    /// <summary>
    /// Из кэша
    /// </summary>
    Cache,

    /// <summary>
    /// От GigaChat API
    /// </summary>
    GigaChat,

    /// <summary>
    /// Локальная fallback рекомендация
    /// </summary>
    Local
}

/// <summary>
/// Интерфейс для оркестратора рекомендаций
/// Координирует работу кэша, GigaChat и fallback сервиса
/// </summary>
public interface IRecommendationOrchestrator
{
    /// <summary>
    /// Получает рекомендацию используя стратегию: кэш → GigaChat → fallback
    /// </summary>
    /// <param name="request">Запрос на рекомендацию</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Результат с рекомендацией</returns>
    Task<RecommendationResult> GetRecommendationAsync(
        OrchestratorRecommendationRequest request,
        CancellationToken ct = default);
}
