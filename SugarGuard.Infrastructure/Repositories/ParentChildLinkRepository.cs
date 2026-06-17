using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class ParentChildLinkRepository : Repository<ParentChildLink>, IParentChildLinkRepository
{
    public ParentChildLinkRepository(DbContext context) : base(context) { }

    public async Task<ParentChildLink?> GetByParentAndChildAsync(
        Guid parentUserId, Guid childId, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(l => l.ParentUserId == parentUserId && l.ChildId == childId, cancellationToken);

    public async Task<bool> ExistsAsync(Guid parentUserId, Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(l => l.ParentUserId == parentUserId && l.ChildId == childId, cancellationToken);

    public async Task<IReadOnlyList<ParentChildLink>> GetByParentAsync(
        Guid parentUserId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(l => l.ParentUserId == parentUserId).ToListAsync(cancellationToken);
}
