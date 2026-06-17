using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IChildRepository : IRepository<Child>
{
    Task<bool> ExistsAsync(Guid childId, CancellationToken cancellationToken = default);
}
