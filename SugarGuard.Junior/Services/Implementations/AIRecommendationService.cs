// Реализация сервиса ИИ рекомендаций
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для ИИ рекомендаций
/// 
/// Логика:
/// 1. Анализирует текущую глюкозу
/// 2. Анализирует тренд (растёт, падает, стабильна)
/// 3. Учитывает состояние ребёнка
/// 4. Рекомендует действия на основе доступных перекусов
/// 5. Кэширует рекомендации для одинаковых ситуаций
/// </summary>
public class AIRecommendationService : IAIRecommendationService
{
    private readonly ILogger<AIRecommendationService> _logger;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IApiClient _apiClient;
    private readonly IRecommendationCacheService _cacheService;

    public AIRecommendationService(
        ILogger<AIRecommendationService> logger,
        IDbContextFactory<AppDbContext> factory,
        IApiClient apiClient,
        IRecommendationCacheService cacheService)
    {
        _logger = logger;
        _factory = factory;
        _apiClient = apiClient;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Получает рекомендацию для текущего состояния
    /// </summary>
    public async Task<AIRecommendation?> GetRecommendationAsync(
        string childId,
        double currentGlucose,
        List<double> recentGlucoseValues,
        string childState,
        List<string> availableSnacks)
    {
        try
        {
            _logger.LogInformation(" Получение рекомендации: глюкоза={Glucose}, состояние={State}", currentGlucose, childState);

            var cached = await _cacheService.FindNearestRecommendation(childId, currentGlucose);
            if (cached != null)
            {
                _logger.LogInformation(" Рекомендация из кэша");
                return cached;
            }

            // Анализируем ситуацию локально (если нет интернета)
            var recommendation = AnalyzeLocally(
                childId,
                currentGlucose,
                recentGlucoseValues,
                childState,
                availableSnacks);

            // Пытаемся получить рекомендацию от API (если интернет есть)
            try
            {
                var apiResponse = await _apiClient.GetRecommendationAsync(
                    new RecommendationRequest
                    {
                        ChildId = childId,
                        CurrentGlucose = currentGlucose,
                        RecentGlucoseValues = recentGlucoseValues,
                        ChildState = childState,
                        AvailableSnacks = availableSnacks
                    });

                 if (apiResponse != null)
                {
                    recommendation = ConvertResponseToRecommendation(apiResponse, childId);
                    _logger.LogInformation("Рекомендация получена от API");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("API недоступен, используем локальную рекомендацию: {Message}", ex.Message);
                recommendation.ModelUsed = "Local";
            }

            // Сохраняем в БД
            await SaveRecommendationAsync(recommendation);

            await _cacheService.AddToCache(childId, currentGlucose, recommendation);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении рекомендации: {Message}", ex.Message);
            throw;
        }
    }

    private AIRecommendation ConvertResponseToRecommendation(
        RecommendationResponse apiResponse,
        string childId)
    {
        var urgency = Enum.TryParse<RecommendationUrgency>(apiResponse.Urgency, ignoreCase: true, out var parsed)
            ? parsed
            : RecommendationUrgency.Normal;

        return new AIRecommendation
        {
            RecommendationId = apiResponse.RecommendationId,
            ChildId = childId,
            RecommendationText = apiResponse.RecommendationText,
            GlucoseValueAtRequest = apiResponse.GlucoseValueAtRequest,
            Urgency = urgency,
            ModelUsed = apiResponse.ModelUsed ?? "API",
            IsFromCache = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Локальный анализ (без интернета)
    /// </summary>
    private AIRecommendation AnalyzeLocally(
    string childId,
    double currentGlucose,
    List<double> recentGlucoseValues,
    string childState,
    List<string> availableSnacks)
    {
        try
        {
            // Измеряем время обработки
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var recommendation = new AIRecommendation
            {
                RecommendationId = Guid.NewGuid().ToString(),
                ChildId = childId,
                GlucoseValueAtRequest = currentGlucose,
                CreatedAt = DateTime.UtcNow,
                ModelUsed = "Local",  // Указываем модель
                IsFromCache = false   // Это не из кэша
            };

            // Определяем тренд
            var trend = GetTrend(recentGlucoseValues);

            // Анализируем по уровню глюкозы используя централизованную классификацию
            var status = GlucoseClassifier.Classify(currentGlucose);
            
            if (status == Models.Enums.GlucoseStatus.CriticallyLow)
            {
                // КРИТИЧЕСКАЯ ГИПОГЛИКЕМИЯ
                recommendation.Urgency = RecommendationUrgency.Critical;
                recommendation.RecommendationText =
                    $" КРИТИЧЕСКАЯ ГИПОГЛИКЕМИЯ! Глюкоза {currentGlucose} ммоль/л.\n" +
                    $"НЕМЕДЛЕННО дайте быстрый углевод (1-2 ХЕ)! Рекомендуем:\n" +
                    $"{GetFastCarbsRecommendation(availableSnacks)}\n\n" +
                    $"Проверьте глюкозу через 10-15 минут.";
            }
            else if (status == Models.Enums.GlucoseStatus.Low)
            {
                // ГИПОГЛИКЕМИЯ
                recommendation.Urgency = RecommendationUrgency.Warning;
                recommendation.RecommendationText =
                    $" ГИПОГЛИКЕМИЯ! Глюкоза {currentGlucose} ммоль/л.\n" +
                    $"Дайте перекус (0.5-1 ХЕ) с быстрыми углеводами:\n" +
                    $"{GetFastCarbsRecommendation(availableSnacks)}";
            }
            else if (status == Models.Enums.GlucoseStatus.CriticallyHigh)
            {
                // КРИТИЧЕСКАЯ ГИПЕРГЛИКЕМИЯ
                recommendation.Urgency = RecommendationUrgency.Critical;
                recommendation.RecommendationText =
                    $" КРИТИЧЕСКАЯ ГИПЕРГЛИКЕМИЯ! Глюкоза {currentGlucose} ммоль/л.\n" +
                    $"Проверьте дозу инсулина. Предложите пить воду.\n" +
                    $"Обратитесь к врачу!";
            }
            else if (status == Models.Enums.GlucoseStatus.High && trend == "rising")
            {
                // РАСТУЩАЯ ГИПЕРГЛИКЕМИЯ
                recommendation.Urgency = RecommendationUrgency.Warning;
                recommendation.RecommendationText =
                    $"� Глюкоза растёт ({currentGlucose} ммоль/л и растёт).\n" +
                    $"Возможно, нужна коррекция инсулина.\n" +
                    $"Предложите воду, спорт может помочь.";
            }
            else if (status == Models.Enums.GlucoseStatus.Normal)
            {
                // НОРМА
                recommendation.Urgency = RecommendationUrgency.Normal;
                recommendation.RecommendationText =
                    $"✅ Отлично! Глюкоза {currentGlucose} ммоль/л в норме.\n" +
                    $"Продолжайте обычный режим.";
            }

            // Обработка случая, если тренд падающий в пограничной зоне
            if (currentGlucose >= GlucoseLevels.TargetRangeMin && currentGlucose < 5.0 && trend == "falling")
            {
                recommendation.Urgency = RecommendationUrgency.Warning;
                recommendation.RecommendationText =
                    $" Глюкоза падает ({currentGlucose} ммоль/л и падает).\n" +
                    $"Будьте готовы к гипогликемии.\n" +
                    $"Держите перекус под рукой.";
            }

            sw.Stop();
            recommendation.LatencyMs = sw.ElapsedMilliseconds;  // Время обработки

            _logger.LogInformation(
                " Локальный анализ завершён: {Urgency}, тренд={Trend}, время={Latency}ms",
                recommendation.Urgency, trend, recommendation.LatencyMs);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при локальном анализе: {Message}", ex.Message);
            throw;
        }
    }


    /// <summary>
    /// Определяет тренд глюкозы
    /// </summary>
    private string GetTrend(List<double> recentValues)
    {
        if (recentValues.Count < 2)
            return "unknown";

        var lastValue = recentValues[^1];
        var prevValue = recentValues[^2];

        if (lastValue > prevValue + 0.5)
            return "rising";
        else if (lastValue < prevValue - 0.5)
            return "falling";
        else
            return "stable";
    }

    /// <summary>
    /// Формирует рекомендацию для быстрых углеводов
    /// </summary>
    private string GetFastCarbsRecommendation(List<string> availableSnacks)
    {
        var fastCarbs = availableSnacks
            .Where(s => IsFastCarb(s))
            .Take(3)
            .ToList();

        if (fastCarbs.Count == 0)
            return "- Сок (1.5 ХЕ / 200мл)\n- Сахар (1 ХЕ / 3-5 кусков)\n- Конфета (0.5 ХЕ)";

        return string.Join("\n", fastCarbs.Select(s => $"- {s}"));
    }

    /// <summary>
    /// Проверяет, является ли перекус "быстрым углеводом"
    /// </summary>
    private bool IsFastCarb(string snackName)
    {
        var fastCarbKeywords = new[] { "сок", "сахар", "конфета", "печенье", "мёд", "компот", "ягода" };
        return fastCarbKeywords.Any(kw => snackName.ToLower().Contains(kw));
    }

    /// <summary>
    /// Получает последнюю рекомендацию
    /// </summary>
    public async Task<AIRecommendation?> GetLatestRecommendationAsync(string childId)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var recommendation = await ctx.Set<AIRecommendation>()
                .AsNoTracking()
                .Where(r => r.ChildId == childId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при получении последней рекомендации: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Сохраняет рекомендацию в БД
    /// </summary>
    public async Task<bool> SaveRecommendationAsync(AIRecommendation recommendation)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<AIRecommendation>().Add(recommendation);
            await ctx.SaveChangesAsync();
            _logger.LogInformation(" Рекомендация сохранена: {RecommendationId}", recommendation.RecommendationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при сохранении рекомендации: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Получает историю рекомендаций
    /// </summary>
    public async Task<List<AIRecommendation>> GetRecommendationHistoryAsync(
        string childId,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var recommendations = await ctx.Set<AIRecommendation>()
                .AsNoTracking()
                .Where(r => r.ChildId == childId &&
                       r.CreatedAt >= startDate &&
                       r.CreatedAt <= endDate)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при получении истории рекомендаций: {Message}", ex.Message);
            return new List<AIRecommendation>();
        }
    }
}
