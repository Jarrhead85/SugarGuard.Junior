namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Обновляет сохранённую в кабинете родителя сводку за текущий день.
/// </summary>
public interface IDailyParentSummaryRefreshService
{
    Task RefreshCurrentDayAsync(Guid childId, CancellationToken cancellationToken = default);
}
