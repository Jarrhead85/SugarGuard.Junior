using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IExportJobRepository : IRepository<ExportJob>
{
    Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<int> CountCompletedTodayAsync(CancellationToken cancellationToken = default);
}
