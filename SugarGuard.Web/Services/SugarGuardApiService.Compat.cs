using SugarGuard.Web.ViewModels;

namespace SugarGuard.Web.Services;

public sealed partial class SugarGuardApiService
{
    /// <summary>
    /// Алиас для GetAdminSystemStatsAsync
    /// </summary>
    public async Task<SystemStatsVm?> GetSystemStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var stats = await GetAdminSystemStatsAsync(cancellationToken);
        return stats is null ? null : MapSystemStats(stats);
    }

    /// <summary>
    /// Алиас для GetAdminUsersAsync
    /// </summary>
    public async Task<List<AdminUserVm>> GetUsersAsync(
        string? role                          = null,
        int     limit                         = 50,
        CancellationToken cancellationToken   = default)
    {
        return await GetAdminUsersAsync(role, limit, cancellationToken);
    }

    private static SystemStatsVm MapAdminSystemStats(AdminSystemStatsDto dto) => new()
    {
        HangfireJobCount         = dto.HangfireActiveJobs,
        TotalUsers               = dto.TotalUsers,
        TotalChildren            = dto.TotalChildren,
        TotalMeasurements        = dto.TotalMeasurements,
        PendingSyncItems         = dto.PendingSyncItems,
        UnresolvedConflicts      = dto.UnresolvedConflicts,
        PendingExportJobs        = dto.PendingExportJobs,
        CompletedExportJobsToday = dto.CompletedExportJobsToday,
        ServerUtcTime            = dto.ServerUtcTime
    };

    private static SystemStatsVm MapSystemStats(AdminSystemStatsVm dto) => new()
    {
        HangfireJobCount         = dto.HangfireActiveJobs,
        TotalUsers               = dto.TotalUsers,
        TotalChildren            = dto.TotalChildren,
        TotalMeasurements        = dto.TotalMeasurements,
        PendingSyncItems         = dto.PendingSyncItems,
        UnresolvedConflicts      = dto.UnresolvedConflicts,
        PendingExportJobs        = dto.PendingExportJobs,
        CompletedExportJobsToday = dto.CompletedExportJobsToday,
        ServerUtcTime            = dto.ServerUtcTime
    };
}
