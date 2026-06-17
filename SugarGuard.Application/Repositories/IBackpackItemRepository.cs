using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IBackpackItemRepository : IRepository<BackpackItem>
{
    Task<IReadOnlyList<BackpackItem>> GetByChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
