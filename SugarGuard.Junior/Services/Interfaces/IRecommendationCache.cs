// Интерфейс для кэша рекомендаций
namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Интерфейс для кэширования рекомендаций по ребёнку и значению глюкозы.
/// Упрощённая версия для оркестратора; кэш разделён по childId.
/// </summary>
public interface IRecommendationCache
{
    /// <summary>
    /// Получает рекомендацию из кэша по ребёнку и значению глюкозы.
    /// </summary>
    /// <param name="childId">ID ребёнка (кэш разделён по детям)</param>
    /// <param name="glucoseValue">Значение глюкозы</param>
    /// <param name="tolerance">Допустимое отклонение (по умолчанию 0.5)</param>
    /// <returns>Текст рекомендации или null если не найдена</returns>
    Task<string?> GetAsync(string childId, double glucoseValue, double tolerance = 0.5);

    /// <summary>
    /// Сохраняет рекомендацию в кэш для данного ребёнка.
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="glucoseValue">Значение глюкозы</param>
    /// <param name="recommendation">Текст рекомендации</param>
    Task SetAsync(string childId, double glucoseValue, string recommendation);
}
