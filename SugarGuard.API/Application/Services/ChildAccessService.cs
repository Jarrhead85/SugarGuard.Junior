using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SugarGuard.API.Data;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services;

/// <summary>
/// Checks whether the current authenticated user can access child data.
/// User identity extraction is owned by <see cref="ICurrentUserContext"/>.
/// </summary>
public sealed class ChildAccessService : IChildAccessService
{
    private static readonly HashSet<UserRole> AdminRoles =
    [
        UserRole.Admin,
        UserRole.SupportAdmin
    ];

    private static readonly TimeSpan RoleCacheTtl = TimeSpan.FromMinutes(1);
    private const string RoleCacheKeyPrefix = "ChildAccess:Role:";

    private readonly ICurrentUserContext _currentUser;
    private readonly AppDbContext _context;
    private readonly IMemoryCache _memoryCache;

    public ChildAccessService(
        ICurrentUserContext currentUser,
        AppDbContext context,
        IMemoryCache memoryCache)
    {
        _currentUser = currentUser;
        _context = context;
        _memoryCache = memoryCache;
    }

    public async Task<bool> CanAccessChildAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        var role = await GetRoleFromDbAsync(userId.Value, cancellationToken);
        if (!role.HasValue)
        {
            return false;
        }

        if (AdminRoles.Contains(role.Value))
        {
            return true;
        }

        return role.Value switch
        {
            UserRole.Doctor => await _context.DoctorChildLinks
                .AsNoTracking()
                .AnyAsync(link => link.DoctorUserId == userId.Value
                                  && link.ChildId == childId
                                  && link.IsActive,
                    cancellationToken),
            UserRole.Parent => await _context.ParentChildLinks
                .AsNoTracking()
                .AnyAsync(link => link.ParentUserId == userId.Value && link.ChildId == childId,
                    cancellationToken),
            UserRole.ChildDevice => await HasChildDeviceAccessAsync(userId.Value, childId, cancellationToken),
            _ => false
        };
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleChildIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue)
        {
            return [];
        }

        var role = await GetRoleFromDbAsync(userId.Value, cancellationToken);
        if (!role.HasValue)
        {
            return [];
        }

        if (AdminRoles.Contains(role.Value))
        {
            return await _context.Children.AsNoTracking()
                .Select(child => child.ChildId)
                .ToListAsync(cancellationToken);
        }

        if (role.Value == UserRole.Doctor)
        {
            return await _context.DoctorChildLinks.AsNoTracking()
                .Where(link => link.DoctorUserId == userId.Value && link.IsActive)
                .Select(link => link.ChildId)
                .ToListAsync(cancellationToken);
        }

        if (role.Value == UserRole.ChildDevice)
        {
            return await GetChildDeviceChildIdsAsync(userId.Value, cancellationToken);
        }

        return await _context.ParentChildLinks.AsNoTracking()
            .Where(link => link.ParentUserId == userId.Value)
            .Select(link => link.ChildId)
            .ToListAsync(cancellationToken);
    }

    private async Task<UserRole?> GetRoleFromDbAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = RoleCacheKeyPrefix + userId;
        if (_memoryCache.TryGetValue(cacheKey, out UserRole cachedRole))
        {
            return cachedRole;
        }

        var role = await _context.Users.AsNoTracking()
            .Where(user => user.UserId == userId && user.IsActive)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (role.HasValue)
        {
            _memoryCache.Set(cacheKey, role.Value, RoleCacheTtl);
        }

        return role;
    }

    private async Task<bool> HasChildDeviceAccessAsync(
        Guid userId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var hasSelfLink = await _context.ParentChildLinks.AsNoTracking()
            .AnyAsync(link => link.ParentUserId == userId
                              && link.ChildId == childId
                              && link.Notes == "Self-link for child mobile account",
                cancellationToken);

        return hasSelfLink;
    }

    private async Task<IReadOnlyList<Guid>> GetChildDeviceChildIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var linkedIds = await _context.ParentChildLinks.AsNoTracking()
            .Where(link => link.ParentUserId == userId
                           && link.Notes == "Self-link for child mobile account")
            .Select(link => link.ChildId)
            .ToListAsync(cancellationToken);

        return linkedIds.Distinct().ToList();
    }
}
