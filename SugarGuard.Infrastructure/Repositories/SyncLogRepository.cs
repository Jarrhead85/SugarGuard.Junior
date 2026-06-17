using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class SyncLogRepository : Repository<SyncLog>, ISyncLogRepository
{
    public SyncLogRepository(DbContext context) : base(context) { }

    public async Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken = default) =>
        await Set.CountAsync(s => s.Status == status, cancellationToken);

    public async Task<int> CountUnresolvedConflictsAsync(CancellationToken cancellationToken = default) =>
        await Set.CountAsync(s => s.IsConflict && s.Status != "resolved", cancellationToken);
}
