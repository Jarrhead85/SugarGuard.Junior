using SugarGuard.API.Application.Ai;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Собирает компактный клинический контекст ребёнка для AI-консультанта.
/// </summary>
public interface IClinicalContextBuilder
{
    /// <summary>
    /// Создаёт контекст для запроса AI.
    /// </summary>
    Task<ClinicalContext> BuildAsync(
        Guid childId,
        Guid? conversationId,
        Guid? measurementId,
        string question,
        CancellationToken cancellationToken);
}
