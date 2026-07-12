using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Оркестрирует запрос AI-рекомендации с памятью, проверкой доступа и аудитом.
/// </summary>
public interface IAiRecommendationWorkflowService
{
    /// <summary>
    /// Создаёт AI-рекомендацию для ребёнка.
    /// </summary>
    Task<RecommendationResponse> CreateRecommendationAsync(
        CreateRecommendationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает последние пользовательские сообщения AI-диалога ребёнка.
    /// </summary>
    Task<AiConversationHistoryResponse> GetConversationHistoryAsync(
        Guid childId,
        int limit,
        CancellationToken cancellationToken);
}
