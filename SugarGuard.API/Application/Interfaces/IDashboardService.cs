using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сводка и история измерений ребёнка для дашборда
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Сводка по ребёнку: последнее измерение и счётчики
    /// </summary>
    Task<DashboardSummaryResponse> GetSummaryAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// История измерений с фильтрами по периоду и UI-состоянию
    /// </summary>
    Task<IReadOnlyList<DashboardHistoryItemResponse>> GetHistoryAsync(
        Guid childId,
        DateTime? from,
        DateTime? to,
        string? uiState,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет существование ребёнка в БД
    /// </summary>
    Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);
}
