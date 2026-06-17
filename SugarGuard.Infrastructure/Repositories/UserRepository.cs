using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(DbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().FirstOrDefaultAsync(u => u.EmailForLogin == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(u => u.Role == role).ToListAsync(cancellationToken);

    public async Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(u => u.EmailForLogin == email, cancellationToken);
}
