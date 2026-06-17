using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IMeasurementRepository : IRepository<Measurement>
{
    Task<Measurement?> GetLatestForChildAsync(Guid childId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Measurement>> GetForChildAsync(Guid childId, DateTime? from, DateTime? to, int limit, CancellationToken cancellationToken = default);
    Task<int> CountForChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
