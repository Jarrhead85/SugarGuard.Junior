// Реализация сервиса кэширования рекомендаций
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для кэширования рекомендаций ИИ
/// Хранит до 50 последних рекомендаций и позволяет искать по ближайшему значению глюкозы
/// Использует SortedDictionary для O(log n) поиска и LRU eviction
/// </summary>
public class RecommendationCacheService : IRecommendationCacheService
{
    private readonly ILogger<RecommendationCacheService> _logger;
    
    // Максимальный размер кэша
    private const int MaxCacheSize = 50;
    
    // Допустимое отклонение для поиска ближайшего значения (±0.3 ммоль/л)
    private const double GlucoseToleranceRange = 0.3;
    
    // Кэш рекомендаций: childId -> SortedDictionary<glucoseValue, CachedRecommendation>
    private readonly Dictionary<string, SortedDictionary<double, CachedRecommendation>> _cache = new();
    
    // Блокировка для thread-safety
    private readonly object _cacheLock = new();
    
    // Статистика кэша
    private int _hitCount = 0;
    private int _missCount = 0;

    public RecommendationCacheService(ILogger<RecommendationCacheService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Добавляет рекомендацию в кэш
    /// </summary>
    public Task AddToCache(string childId, double glucoseValue, AIRecommendation recommendation)
    {
        try
        {
            _logger.LogInformation("Добавление рекомендации в кэш: ребёнок {ChildId}, глюкоза {Glucose}", 
                childId, glucoseValue);

            var cachedItem = new CachedRecommendation
            {
                ChildId = childId,
                GlucoseValue = glucoseValue,
                Recommendation = recommendation,
                CachedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            lock (_cacheLock)
            {
                // Получаем или создаём SortedDictionary для ребёнка
                if (!_cache.TryGetValue(childId, out var childCache))
                {
                    childCache = new SortedDictionary<double, CachedRecommendation>();
                    _cache[childId] = childCache;
                }

                // Добавляем или обновляем рекомендацию
                childCache[glucoseValue] = cachedItem;
                
                _logger.LogInformation("Рекомендация добавлена в кэш");

                // Проверяем лимит размера кэша и применяем LRU eviction
                if (childCache.Count > MaxCacheSize)
                {
                    // Находим запись с самым старым LastAccessedAt
                    var oldestEntry = childCache
                        .OrderBy(kvp => kvp.Value.LastAccessedAt)
                        .First();
                    
                    childCache.Remove(oldestEntry.Key);
                    
                    _logger.LogInformation("Удалена старая рекомендация из кэша (LRU, лимит {MaxSize})", MaxCacheSize);
                }

                _logger.LogInformation("Размер кэша для ребёнка {ChildId}: {CacheSize}", childId, childCache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при добавлении рекомендации в кэш");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ищет рекомендацию в кэше по ближайшему значению глюкозы (±0.3)
    /// Использует SortedDictionary для эффективного поиска O(log n)
    /// </summary>
    public Task<AIRecommendation?> FindNearestRecommendation(string childId, double glucoseValue)
    {
        try
        {
            _logger.LogInformation("Поиск рекомендации в кэше: ребёнок {ChildId}, глюкоза {Glucose}", 
                childId, glucoseValue);

            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(childId, out var childCache) || childCache.Count == 0)
                {
                    RecordCacheMiss();
                    _logger.LogInformation("Кэш пуст для ребёнка {ChildId}", childId);
                    return Task.FromResult<AIRecommendation?>(null);
                }

                CachedRecommendation? nearestItem = null;
                double minDifference = double.MaxValue;

                // Ищем ближайшее значение в допустимом диапазоне
                // SortedDictionary позволяет эффективно искать в отсортированном порядке
                foreach (var kvp in childCache)
                {
                    var difference = Math.Abs(kvp.Key - glucoseValue);
                    
                    // Если разница больше допустимой и мы уже прошли целевое значение,
                    // можем прекратить поиск (оптимизация для отсортированного словаря)
                    if (kvp.Key > glucoseValue + GlucoseToleranceRange)
                    {
                        break;
                    }
                    
                    if (difference <= GlucoseToleranceRange && difference < minDifference)
                    {
                        minDifference = difference;
                        nearestItem = kvp.Value;
                    }
                }

                // Обновляем время последнего доступа для LRU
                if (nearestItem != null)
                {
                    nearestItem.LastAccessedAt = DateTime.UtcNow;
                    RecordCacheHit();
                    
                    // Помечаем рекомендацию как из кэша
                    var cachedRecommendation = nearestItem.Recommendation;
                    cachedRecommendation.IsFromCache = true;
                    
                    _logger.LogInformation("Найдена рекомендация в кэше: глюкоза {CachedGlucose} (разница {Difference:F2})", 
                        nearestItem.GlucoseValue, minDifference);
                    
                    return Task.FromResult<AIRecommendation?>(cachedRecommendation);
                }

                RecordCacheMiss();
                _logger.LogInformation("Рекомендация не найдена в кэше (диапазон ±{Tolerance})", GlucoseToleranceRange);
                return Task.FromResult<AIRecommendation?>(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при поиске рекомендации в кэше");
            RecordCacheMiss();
            return Task.FromResult<AIRecommendation?>(null);
        }
    }

    /// <summary>
    /// Получает количество элементов в кэше для ребёнка
    /// </summary>
    public Task<int> GetCacheSize(string childId)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(childId, out var childCache))
            {
                return Task.FromResult(childCache.Count);
            }
            
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// Очищает кэш для ребёнка
    /// </summary>
    public Task ClearCache(string childId)
    {
        try
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(childId, out var childCache))
                {
                    var count = childCache.Count;
                    _cache.Remove(childId);
                    _logger.LogInformation("Очищен кэш для ребёнка {ChildId}, удалено {Count} элементов", 
                        childId, count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке кэша для ребёнка {ChildId}", childId);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Очищает весь кэш
    /// </summary>
    public Task ClearAllCache()
    {
        try
        {
            lock (_cacheLock)
            {
                var totalItems = _cache.Values.Sum(cache => cache.Count);
                _cache.Clear();
                
                // Сбрасываем статистику
                _hitCount = 0;
                _missCount = 0;
                
                _logger.LogInformation("Очищен весь кэш, удалено {Count} элементов", totalItems);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке всего кэша");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Получает статистику кэша
    /// </summary>
    public Task<CacheStatistics> GetCacheStatistics()
    {
        lock (_cacheLock)
        {
            var totalItems = _cache.Values.Sum(cache => cache.Count);
            
            return Task.FromResult(new CacheStatistics
            {
                TotalItems = totalItems,
                HitCount = _hitCount,
                MissCount = _missCount,
                LastUpdated = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Записывает попадание в кэш
    /// </summary>
    private void RecordCacheHit()
    {
        // Вызывается внутри _cacheLock, поэтому дополнительная блокировка не нужна
        _hitCount++;
    }

    /// <summary>
    /// Записывает промах кэша
    /// </summary>
    private void RecordCacheMiss()
    {
        // Вызывается внутри _cacheLock, поэтому дополнительная блокировка не нужна
        _missCount++;
    }
}
