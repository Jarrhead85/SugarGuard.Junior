using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IParentChildLinkRepository : IRepository<ParentChildLink>
{
    Task<ParentChildLink?> GetByParentAndChildAsync(Guid parentUserId, Guid childId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid parentUserId, Guid childId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParentChildLink>> GetByParentAsync(Guid parentUserId, CancellationToken cancellationToken = default);
}
