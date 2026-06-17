using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SugarGuard.API.Data;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Реализация провенрки к данным ребенка
    /// </summary>

    public class ChildAccessService : IChildAccessService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _context;
        private readonly IMemoryCache _memoryCache;

        /// <summary>
        /// Множество ролей с доступом ко всем детям без проверки связки
        /// </summary>
        private static readonly HashSet<UserRole> _adminRoles = new()
        {
            UserRole.Admin,
            UserRole.SupportAdmin,
            UserRole.ServiceAccount
        };

        /// <summary>
        /// Префикс ключа кеша роли пользователя
        /// </summary>
        private const string RoleCacheKeyPrefix = "ChildAccess:Role:";

        /// <summary>
        /// Префикс ключа кеша проверки доступа к ребёнку
        /// </summary>
        private const string CanAccessCacheKeyPrefix = "ChildAccess:CanAccess:";

        /// <summary>
        /// request кеша роли
        /// </summary>
        private static readonly TimeSpan RoleCacheTtl = TimeSpan.FromMinutes(10);

        public ChildAccessService(
            IHttpContextAccessor httpContextAccessor,
            AppDbContext context,
            IMemoryCache memoryCache)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _memoryCache = memoryCache;
        }

        // Вспомогательные методы чтения claims 
        /// <inheritdoc/>
        public Guid? GetCurrentUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                            ?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? _httpContextAccessor.HttpContext?.User
                            ?.FindFirstValue("UserId");

            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
                return null;

            return userId;
        }

        /// <inheritdoc/>
        public UserRole? GetCurrentUserRole()
        {
            var roleClaim = _httpContextAccessor.HttpContext?.User
                                ?.FindFirstValue(ClaimTypes.Role)
                            ?? _httpContextAccessor.HttpContext?.User
                                ?.FindFirstValue("role");

            if (string.IsNullOrEmpty(roleClaim))
                return null;

            return Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role)
                ? role
                : null;
        }

        private UserRole? GetCachedRole(Guid userId)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.Items.TryGetValue(RoleCacheKeyPrefix + userId, out var v) == true
                && v is UserRole r)
            {
                return r;
            }
            return null;
        }

        private void SetCachedRole(Guid userId, UserRole role)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is not null)
            {
                ctx.Items[RoleCacheKeyPrefix + userId] = role;
            }
        }

        private bool? GetCachedCanAccess(Guid userId, Guid childId)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.Items.TryGetValue(CanAccessCacheKeyPrefix + userId + ":" + childId, out var v) == true
                && v is bool b)
            {
                return b;
            }
            return null;
        }

        private void SetCachedCanAccess(Guid userId, Guid childId, bool result)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is not null)
            {
                ctx.Items[CanAccessCacheKeyPrefix + userId + ":" + childId] = result;
            }
        }

        /// <summary>
        /// Загружает роль пользователя из БД
        /// </summary>
        private async Task<UserRole?> GetRoleFromDbAsync(Guid userId, CancellationToken cancellationToken)
        {
            var cached = GetCachedRole(userId);
            if (cached.HasValue)
                return cached;

            var memoryKey = RoleCacheKeyPrefix + userId;
            if (_memoryCache.TryGetValue(memoryKey, out UserRole? memoryCached)
                && memoryCached.HasValue)
            {
                SetCachedRole(userId, memoryCached.Value);
                return memoryCached;
            }

            var role = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => (UserRole?)u.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (role.HasValue)
            {
                SetCachedRole(userId, role.Value);
                _memoryCache.Set(memoryKey, role.Value, RoleCacheTtl);
            }
            return role;
        }

        // Основные методы доступа
        /// <inheritdoc/>
        public async Task<bool> CanAccessChildAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return false;

            var cached = GetCachedCanAccess(userId.Value, childId);
            if (cached.HasValue)
                return cached.Value;

            var role = await GetRoleFromDbAsync(userId.Value, cancellationToken);
            if (!role.HasValue)
                return CacheAndReturn(userId.Value, childId, false);

            // Admin-роли видят всех детей
            if (_adminRoles.Contains(role.Value))
                return CacheAndReturn(userId.Value, childId, true);

            // Проверяем только нужную таблицу в зависимости от роли
            bool hasLink = role.Value switch
            {
                UserRole.Doctor => await _context.DoctorChildLinks
                    .AsNoTracking()
                    .AnyAsync(l => l.DoctorUserId == userId.Value
                                   && l.ChildId == childId
                                   && l.IsActive,
                              cancellationToken),
                UserRole.Parent => await _context.ParentChildLinks
                    .AsNoTracking()
                    .AnyAsync(l => l.ParentUserId == userId.Value
                                   && l.ChildId == childId,
                              cancellationToken),
                _ => false
            };

            return CacheAndReturn(userId.Value, childId, hasLink);
        }

        private bool CacheAndReturn(Guid userId, Guid childId, bool result)
        {
            SetCachedCanAccess(userId, childId, result);
            return result;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Guid>> GetAccessibleChildIdsAsync(
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Array.Empty<Guid>();

            var role = await GetRoleFromDbAsync(userId.Value, cancellationToken);
            if (!role.HasValue)
                return Array.Empty<Guid>();

            // Admin видит всех
            if (_adminRoles.Contains(role.Value))
                return await _context.Children
                    .AsNoTracking()
                    .Select(c => c.ChildId)
                    .ToListAsync(cancellationToken);

            if (role.Value == UserRole.Doctor)
                return await _context.DoctorChildLinks
                    .AsNoTracking()
                    .Where(l => l.DoctorUserId == userId.Value && l.IsActive)
                    .Select(l => l.ChildId)
                    .ToListAsync(cancellationToken);

            // Parent
            return await _context.ParentChildLinks
                .AsNoTracking()
                .Where(l => l.ParentUserId == userId.Value)
                .Select(l => l.ChildId)
                .ToListAsync(cancellationToken);
        }
    }
}
