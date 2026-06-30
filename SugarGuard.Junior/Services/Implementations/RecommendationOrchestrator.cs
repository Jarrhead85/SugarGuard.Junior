using Microsoft.Extensions.Logging;
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using System.Diagnostics;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Оркестратор рекомендаций — координирует кэш, API (GigaChat через backend) и fallback.
/// Стратегия: кэш → API (серверный GigaChat) → fallback
/// </summary>
public class RecommendationOrchestrator : IRecommendationOrchestrator
{
    private readonly IRecommendationCache _cache;
    private readonly IApiClient _apiClient;
    private readonly IFallbackRecommendationService _fallback;
    private readonly IChildRepository _childRepository;
    private readonly ILogger<RecommendationOrchestrator> _logger;

    public RecommendationOrchestrator(
        IRecommendationCache cache,
        IApiClient apiClient,
        IFallbackRecommendationService fallback,
        IChildRepository childRepository,
        ILogger<RecommendationOrchestrator> logger)
    {
        _cache = cache;
        _apiClient = apiClient;
        _fallback = fallback;
        _childRepository = childRepository;
        _logger = logger;
    }

    public async Task<RecommendationResult> GetRecommendationAsync(
        OrchestratorRecommendationRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Запрос рекомендации: ребёнок={ChildId}, глюкоза={Glucose}",
                request.ChildId, request.GlucoseValue);

            var status = GlucoseClassifier.Classify(request.GlucoseValue);

            // Этап 1: Проверить кэш. Для опасных значений нужен свежий ответ,
            // чтобы ребёнок не получил старую мягкую рекомендацию.
            var cached = status == SugarGuard.Junior.Models.Enums.GlucoseStatus.Normal
                ? await _cache.GetAsync(request.ChildId, request.GlucoseValue)
                : null;
            if (cached != null)
            {
                stopwatch.Stop();
                _logger.LogInformation("Рекомендация из кэша за {Latency}ms", stopwatch.ElapsedMilliseconds);

                return new RecommendationResult
                {
                    Text = cached,
                    Source = RecommendationSource.Cache,
                    IsFromCache = true,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Этап 2: Запросить API (прокси -> server-side GigaChat) с таймаутом 5 секунд
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var apiResponse = await _apiClient.GetRecommendationAsync(
                    new RecommendationRequest
                    {
                        ChildId = request.ChildId,
                        CurrentGlucose = request.GlucoseValue,
                        RecentGlucoseValues = request.RecentGlucoseValues,
                        ChildState = request.ChildState,
                        AvailableSnacks = request.AvailableSnacks
                    });

                if (apiResponse != null)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("Рекомендация от API за {Latency}ms", stopwatch.ElapsedMilliseconds);

                    await _cache.SetAsync(request.ChildId, request.GlucoseValue, apiResponse.RecommendationText);

                    return new RecommendationResult
                    {
                        Text = apiResponse.RecommendationText,
                        Source = RecommendationSource.GigaChat,
                        IsFromCache = false,
                        LatencyMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("API timeout для глюкозы {Glucose}", request.GlucoseValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка API рекомендации");
            }

            // Этап 3: Fallback на локальную рекомендацию
            var child = await _childRepository.GetByIdAsync(request.ChildId);
            if (child == null)
            {
                stopwatch.Stop();
                _logger.LogWarning("Ребёнок {ChildId} не найден", request.ChildId);

                return new RecommendationResult
                {
                    Text = "Ошибка: профиль ребёнка не найден",
                    Source = RecommendationSource.Local,
                    IsFromCache = false,
                    Message = "Профиль ребёнка не найден",
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }

            var localRec = _fallback.GetRecommendation(status, child, request.AvailableSnacks);

            stopwatch.Stop();
            _logger.LogInformation("Локальная рекомендация за {Latency}ms", stopwatch.ElapsedMilliseconds);

            return new RecommendationResult
            {
                Text = localRec,
                Source = RecommendationSource.Local,
                IsFromCache = false,
                Message = "Локальная рекомендация (API недоступен)",
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Критическая ошибка в оркестраторе");

            return new RecommendationResult
            {
                Text = "Произошла ошибка при получении рекомендации. Обратитесь к врачу.",
                Source = RecommendationSource.Local,
                IsFromCache = false,
                Message = $"Ошибка: {ex.Message}",
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
