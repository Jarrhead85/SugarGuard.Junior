using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class BackpackItemRepository : Repository<BackpackItem>, IBackpackItemRepository
{
    public BackpackItemRepository(DbContext context) : base(context) { }

    public async Task<IReadOnlyList<BackpackItem>> GetByChildAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(b => b.ChildId == childId).ToListAsync(cancellationToken);
}
