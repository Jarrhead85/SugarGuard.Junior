using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика ИИ-рекомендаций
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Ищет недавнюю кэш-рекомендацию для ребёнка
    /// </summary>
    Task<AIRecommendation?> FindCachedRecommendationAsync(
        Guid childId,
        decimal glucoseValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает до 10 последних замеров за последние 3 часа
    /// </summary>
    Task<List<Measurement>> GetRecentMeasurementsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает названия доступных снеков из рюкзака ребёнка
    /// </summary>
    Task<List<string>> GetAvailableSnacksAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Строит запрос к GigaChat
    /// </summary>
    GigaChatRequest BuildGigaChatRequest(
        Child child,
        decimal glucoseValue,
        List<Measurement> recentMeasurements,
        List<string> availableSnacks);

    /// <summary>
    /// Загружает ребёнка
    /// </summary>
    Task<Child?> GetChildWithSettingsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет новую рекомендацию от GigaChat и привязывает её к измерению
    /// </summary>
    Task<AIRecommendation> SaveRecommendationAsync(
        Guid childId,
        Guid? measurementId,
        decimal glucoseValue,
        GigaChatResponse gigaChatResponse,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает рекомендацию по её ID
    /// </summary>
    Task<AIRecommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу истории рекомендаций ребёнка
    /// </summary>
    Task<RecommendationHistoryPage> GetHistoryAsync(
        Guid childId,
        int limit,
        DateTime? from,
        DateTime? to,
        DateTime? cursor,
        CancellationToken cancellationToken = default);
}
