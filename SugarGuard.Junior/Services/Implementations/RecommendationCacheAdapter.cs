// Адаптер для IRecommendationCache, использующий существующий IRecommendationCacheService
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Адаптер для IRecommendationCache, использующий IRecommendationCacheService.
/// Кэш разделён по childId — рекомендации разных детей не смешиваются.
/// </summary>
public class RecommendationCacheAdapter : IRecommendationCache
{
    private readonly IRecommendationCacheService _cacheService;

    public RecommendationCacheAdapter(IRecommendationCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <summary>
    /// Получает рекомендацию из кэша по ребёнку и значению глюкозы.
    /// </summary>
    public async Task<string?> GetAsync(string childId, double glucoseValue, double tolerance = 0.5)
    {
        var recommendation = await _cacheService.FindNearestRecommendation(childId, glucoseValue);
        return recommendation?.RecommendationText;
    }

    /// <summary>
    /// Сохраняет рекомендацию в кэш для данного ребёнка.
    /// </summary>
    public async Task SetAsync(string childId, double glucoseValue, string recommendation)
    {
        var aiRecommendation = new AIRecommendation
        {
            RecommendationId = Guid.NewGuid().ToString(),
            ChildId = childId,
            GlucoseValueAtRequest = glucoseValue,
            RecommendationText = recommendation,
            Urgency = RecommendationUrgency.Normal,
            ModelUsed = "Cache",
            IsFromCache = false,
            CreatedAt = DateTime.UtcNow
        };

        await _cacheService.AddToCache(childId, glucoseValue, aiRecommendation);
    }
}
