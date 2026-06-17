using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class ExportJobRepository : Repository<ExportJob>, IExportJobRepository
{
    public ExportJobRepository(DbContext context) : base(context) { }

    public async Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken = default) =>
        await Set.CountAsync(j => j.Status == status, cancellationToken);

    public async Task<int> CountCompletedTodayAsync(CancellationToken cancellationToken = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        return await Set.CountAsync(
            j => j.Status == "completed" && j.CompletedAt >= todayStart, cancellationToken);
    }
}
