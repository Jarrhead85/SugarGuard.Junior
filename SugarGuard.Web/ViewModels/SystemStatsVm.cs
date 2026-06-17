// SugarGuard.Web/ViewModels/SystemStatsVm.cs
// Alias-обёртка для совместимости со старым кодом Admin/Dashboard.razor.
// Страница использует SystemStatsVm и поле HangfireJobCount.

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// Системная статистика Admin-панели.
/// Используется в Admin/Dashboard.razor.
/// </summary>
public sealed class SystemStatsVm
{
    public int      HangfireJobCount         { get; init; }  // HangfireActiveJobs в API
    public int      TotalUsers               { get; init; }
    public int      TotalChildren            { get; init; }
    public long     TotalMeasurements        { get; init; }
    public int      PendingSyncItems         { get; init; }
    public int      UnresolvedConflicts      { get; init; }
    public int      PendingExportJobs        { get; init; }
    public int      CompletedExportJobsToday { get; init; }
    public DateTime ServerUtcTime            { get; init; }
}
