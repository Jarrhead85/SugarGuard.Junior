using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class InviteCodeRepository : Repository<InviteCode>, IInviteCodeRepository
{
    public InviteCodeRepository(DbContext context) : base(context) { }

    public async Task<InviteCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public async Task<IReadOnlyList<InviteCode>> GetActiveForChildAsync(
        Guid childId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await Set.AsNoTracking()
            .Where(c => c.ChildId == childId && c.Status == "Pending" && c.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }
}
