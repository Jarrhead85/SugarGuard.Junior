// Интерфейс для кэширования рекомендаций

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Элемент кэша рекомендаций
/// </summary>
public class CachedRecommendation
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Значение глюкозы при запросе
    /// </summary>
    public double GlucoseValue { get; set; }

    /// <summary>
    /// Рекомендация
    /// </summary>
    public AIRecommendation Recommendation { get; set; } = new();

    /// <summary>
    /// Время создания кэша
    /// </summary>
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время последнего доступа (для LRU eviction)
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Интерфейс для кэширования рекомендаций
/// </summary>
public interface IRecommendationCacheService
{
    /// <summary>
    /// Добавляет рекомендацию в кэш
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="glucoseValue">Значение глюкозы</param>
    /// <param name="recommendation">Рекомендация для кэширования</param>
    Task AddToCache(string childId, double glucoseValue, AIRecommendation recommendation);

    /// <summary>
    /// Ищет рекомендацию в кэше по ближайшему значению глюкозы (±0.5)
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="glucoseValue">Значение глюкозы для поиска</param>
    /// <returns>Рекомендация из кэша или null если не найдена</returns>
    Task<AIRecommendation?> FindNearestRecommendation(string childId, double glucoseValue);

    /// <summary>
    /// Получает количество элементов в кэше для ребёнка
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <returns>Количество элементов в кэше</returns>
    Task<int> GetCacheSize(string childId);

    /// <summary>
    /// Очищает кэш для ребёнка
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    Task ClearCache(string childId);

    /// <summary>
    /// Очищает весь кэш
    /// </summary>
    Task ClearAllCache();

    /// <summary>
    /// Получает статистику кэша
    /// </summary>
    /// <returns>Статистика использования кэша</returns>
    Task<CacheStatistics> GetCacheStatistics();
}

/// <summary>
/// Статистика кэша рекомендаций
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Общее количество элементов в кэше
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Количество попаданий в кэш
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// Количество промахов кэша
    /// </summary>
    public int MissCount { get; set; }

    /// <summary>
    /// Процент попаданий в кэш
    /// </summary>
    public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) * 100 : 0;

    /// <summary>
    /// Время последнего обновления статистики
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}