using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface ISyncLogRepository : IRepository<SyncLog>
{
    Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<int> CountUnresolvedConflictsAsync(CancellationToken cancellationToken = default);
}
