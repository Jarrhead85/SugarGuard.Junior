using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(DbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = await Set.Where(t => t.ExpiresAt < now).ToListAsync(cancellationToken);
        var count = expired.Count;
        if (count > 0)
        {
            Set.RemoveRange(expired);
            await Context.SaveChangesAsync(cancellationToken);
        }
        return count;
    }
}
