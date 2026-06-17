using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class ChildRepository : Repository<Child>, IChildRepository
{
    public ChildRepository(DbContext context) : base(context) { }

    public async Task<bool> ExistsAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(c => c.ChildId == childId, cancellationToken);
}
