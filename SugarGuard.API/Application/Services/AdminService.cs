using Hangfire;
using Hangfire.Storage.Monitoring;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Enums;
using SugarGuard.API.Security;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация административного сервиса
/// </summary>
public sealed class AdminService : IAdminService
{
    private readonly IUserRepository _users;
    private readonly IChildRepository _children;
    private readonly IParentChildLinkRepository _parentChildLinks;
    private readonly IDoctorChildLinkRepository _doctorChildLinks;
    private readonly IInviteCodeRepository _inviteCodes;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOnboardingEventRepository _onboardingEvents;
    private readonly IMeasurementRepository _measurements;
    private readonly ISyncLogRepository _syncLogs;
    private readonly IExportJobRepository _exportJobs;
    private readonly IInviteCodeService _inviteCodeService;
    private readonly IAuditService _audit;
    private readonly ILogger<AdminService> _logger;
    private readonly ICryptoService _crypto;

    /// <summary>
    /// Допустимые роли, которым можно назначить связь врач-ребёнок
    /// </summary>
    private static readonly HashSet<UserRole> AllowedDoctorRoles = new()
    {
        UserRole.Doctor,
        UserRole.Admin,
        UserRole.SupportAdmin
    };

    // Учётные записи интеграций выдаются только при серверном provisioning, не через UI.
    private static readonly HashSet<UserRole> AllowedAssignableRoles = new()
    {
        UserRole.Parent,
        UserRole.Doctor,
        UserRole.Admin,
        UserRole.SupportAdmin,
        UserRole.ChildDevice
    };

    public AdminService(
        IUserRepository users,
        IChildRepository children,
        IParentChildLinkRepository parentChildLinks,
        IDoctorChildLinkRepository doctorChildLinks,
        IInviteCodeRepository inviteCodes,
        IAuditLogRepository auditLogs,
        IOnboardingEventRepository onboardingEvents,
        IMeasurementRepository measurements,
        ISyncLogRepository syncLogs,
        IExportJobRepository exportJobs,
        IInviteCodeService inviteCodeService,
        IAuditService audit,
        ICryptoService crypto,
        ILogger<AdminService> logger)
    {
        _users = users;
        _children = children;
        _parentChildLinks = parentChildLinks;
        _doctorChildLinks = doctorChildLinks;
        _inviteCodes = inviteCodes;
        _auditLogs = auditLogs;
        _onboardingEvents = onboardingEvents;
        _measurements = measurements;
        _syncLogs = syncLogs;
        _exportJobs = exportJobs;
        _inviteCodeService = inviteCodeService;
        _audit = audit;
        _crypto = crypto;
        _logger = logger;
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync(
        UserRole? role,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);

        IQueryable<User> query = _users.Query()
            .OrderByDescending(u => u.CreatedAt);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        var users = await query
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        return users.Select(MapAdminUser).ToList();
    }

    public async Task<PagedResult<AdminUserResponse>> GetUsersPageAsync(
        UserRole? role,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 10, 100);
        IQueryable<User> query = _users.Query().AsNoTracking();

        if (role.HasValue)
        {
            query = query.Where(user => user.Role == role.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            if (Guid.TryParse(searchTerm, out var userId))
            {
                query = query.Where(user => user.UserId == userId);
            }
            else
            {
                var roleMatches = Enum.TryParse<UserRole>(searchTerm, ignoreCase: true, out var parsedRole);
                query = query.Where(user =>
                    (user.EmailForLogin != null && EF.Functions.ILike(user.EmailForLogin, $"%{searchTerm}%"))
                    || (user.TelegramId.HasValue && user.TelegramId.Value.ToString().Contains(searchTerm))
                    || (roleMatches && user.Role == parsedRole));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.UserId)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserResponse>
        {
            Items = users.Select(MapAdminUser).ToArray(),
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<List<ChildResponse>> GetAllChildrenAsync(
        CancellationToken cancellationToken = default)
    {
        return await _children.Query()
            .AsNoTracking()
            .OrderBy(child => child.FirstName)
            .ThenBy(child => child.LastName)
            .Select(child => new ChildResponse
            {
                ChildId = child.ChildId,
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = child.DateOfBirth,
                Weight = child.Weight,
                Height = child.Height,
                DiabetesType = child.DiabetesType,
                DiagnosisDate = child.DiagnosisDate,
                InsulinScheme = child.InsulinScheme,
                CurrentInsulins = child.CurrentInsulins,
                TimeZoneId = child.TimeZoneId,
                CreatedAt = child.CreatedAt,
                UpdatedAt = child.UpdatedAt,
                PhotoUrl = child.PhotoUrl
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminUserResponse?> UpdateUserRoleAsync(
        Guid userId,
        string newRole,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(newRole, ignoreCase: true, out var parsedRole))
            throw new ArgumentException($"Unknown role '{newRole}'.");

        if (!AllowedAssignableRoles.Contains(parsedRole))
            throw new ArgumentException($"Role '{parsedRole}' cannot be assigned manually.");

        var user = await _users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("UpdateUserRole: пользователь {UserId} не найден.", userId);
            return null;
        }

        var oldRole = user.Role;

        if (oldRole == UserRole.Admin && parsedRole != UserRole.Admin && user.IsActive)
        {
            var activeAdministrators = await _users.Query()
                .CountAsync(candidate => candidate.Role == UserRole.Admin && candidate.IsActive, cancellationToken);

            if (activeAdministrators <= 1)
                throw new InvalidOperationException("Нельзя снять роль с последнего активного администратора.");
        }

        user.Role = parsedRole;
        _users.Update(user);
        await _users.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "admin.rolechanged",
            targetType: "User",
            targetId: user.UserId.ToString(),
            details: $"{oldRole} → {parsedRole}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Роль пользователя {UserId} изменена: {OldRole} → {NewRole}.",
            userId, oldRole, parsedRole);

        return MapAdminUser(user);
    }

    private AdminUserResponse MapAdminUser(User user)
    {
        var firstName = Decrypt(user.EncryptedFirstName);
        var lastName = Decrypt(user.EncryptedLastName);
        var displayName = string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new AdminUserResponse
        {
            UserId = user.UserId,
            EmailForLogin = user.EmailForLogin,
            TelegramId = user.TelegramId,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            DisplayName = displayName,
            PhotoUrl = user.ProfilePhotoUrl,
            Specialty = user.DoctorSpecialty,
            // Полный номер лицензии доступен только в карточке заявки на верификацию.
            LicenseNumber = MaskLicenseNumber(Decrypt(user.EncryptedDoctorLicense)),
            IsEmailVerified = user.IsEmailVerified
        };
    }

    private string Decrypt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            return _crypto.Decrypt(value);
        }
        catch (Exception exception) when (exception is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return value;
        }
    }

    private static string MaskLicenseNumber(string licenseNumber)
    {
        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            return string.Empty;
        }

        var visibleLength = Math.Min(4, licenseNumber.Length);
        return $"••••{licenseNumber[^visibleLength..]}";
    }

    public async Task<bool> DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("DeactivateUser: пользователь {UserId} не найден.", userId);
            return false;
        }

        if (!user.IsActive)
        {
            _logger.LogInformation(
                "DeactivateUser: пользователь {UserId} уже деактивирован.", userId);
            return true;
        }

        user.IsActive = false;
        user.DeactivatedAt = DateTime.UtcNow;
        _users.Update(user);
        await _users.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "admin.userdeactivated",
            targetType: "User",
            targetId: user.UserId.ToString(),
            details: $"role:{user.Role}",
            cancellationToken: cancellationToken);

        _logger.LogInformation("Пользователь {UserId} деактивирован.", userId);
        return true;
    }

    public async Task<int> SetUsersActivityAsync(
        IReadOnlyCollection<Guid> userIds,
        bool isActive,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var distinctUserIds = userIds.Distinct().ToArray();
        if (distinctUserIds.Length is < 1 or > 100)
        {
            throw new ArgumentException("Нужно выбрать от 1 до 100 пользователей.");
        }

        if (!isActive && distinctUserIds.Contains(actorUserId))
        {
            throw new InvalidOperationException("Нельзя деактивировать собственную учётную запись.");
        }

        var users = await _users.Query()
            .Where(user => distinctUserIds.Contains(user.UserId))
            .ToListAsync(cancellationToken);

        if (users.Count != distinctUserIds.Length)
        {
            throw new ArgumentException("Один или несколько пользователей не найдены.");
        }

        if (!isActive)
        {
            var activeAdministrators = await _users.Query()
                .CountAsync(user => user.Role == UserRole.Admin && user.IsActive, cancellationToken);
            var administratorsToDeactivate = users.Count(user => user.Role == UserRole.Admin && user.IsActive);

            if (activeAdministrators - administratorsToDeactivate <= 0)
            {
                throw new InvalidOperationException("Нельзя деактивировать последнего активного администратора.");
            }
        }

        var now = DateTime.UtcNow;
        foreach (var user in users)
        {
            user.IsActive = isActive;
            user.DeactivatedAt = isActive ? null : now;
        }

        await _users.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: isActive ? "admin.usersactivated" : "admin.usersdeactivated",
            targetType: "Users",
            targetId: string.Join(',', distinctUserIds),
            details: $"count:{users.Count}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Администратор изменил статус {Count} пользователей. IsActive={IsActive}.",
            users.Count,
            isActive);

        return users.Count;
    }

    public async Task CreateParentChildLinkAsync(
        Guid parentUserId,
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        var parentExists = await _users.AnyAsync(u => u.UserId == parentUserId, cancellationToken);
        var childExists = await _children.AnyAsync(c => c.ChildId == childId, cancellationToken);

        if (!parentExists || !childExists)
            throw new ArgumentException(
                $"Родитель {parentUserId} или ребёнок {childId} не найдены.");

        var linkExists = await _parentChildLinks.AnyAsync(
            l => l.ParentUserId == parentUserId && l.ChildId == childId, cancellationToken);

        if (linkExists)
            throw new InvalidOperationException(
                $"Связь родитель {parentUserId} — ребёнок {childId} уже существует.");

        _parentChildLinks.Add(new ParentChildLink
        {
            ParentUserId = parentUserId,
            ChildId = childId,
            CreatedAt = DateTime.UtcNow
        });

        await _parentChildLinks.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "admin.parentchildlinkcreated",
            targetType: "ParentChildLink",
            targetId: $"{parentUserId}:{childId}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Создана связь родитель {ParentId} — ребёнок {ChildId}.",
            parentUserId, childId);
    }

    public async Task<bool> RemoveParentChildLinkAsync(
        Guid parentUserId,
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        var link = await _parentChildLinks.FirstOrDefaultAsync(
            l => l.ParentUserId == parentUserId && l.ChildId == childId,
            cancellationToken);

        if (link is null)
        {
            _logger.LogWarning(
                "RemoveParentChildLink: связь {ParentId}:{ChildId} не найдена.",
                parentUserId, childId);
            return false;
        }

        _parentChildLinks.Remove(link);
        await _parentChildLinks.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "admin.parentchildlinkdeleted",
            targetType: "ParentChildLink",
            targetId: $"{parentUserId}:{childId}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Удалена связь родитель {ParentId} — ребёнок {ChildId}.",
            parentUserId, childId);

        return true;
    }

    public async Task CreateDoctorChildLinkAsync(
        Guid doctorUserId,
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _users.FirstOrDefaultAsync(u => u.UserId == doctorUserId, cancellationToken);
        var childExists = await _children.AnyAsync(c => c.ChildId == childId, cancellationToken);

        if (doctor is null || !childExists)
            throw new ArgumentException(
                $"Врач {doctorUserId} или ребёнок {childId} не найдены.");

        if (!AllowedDoctorRoles.Contains(doctor.Role))
            throw new ArgumentException(
                $"Пользователь {doctorUserId} с ролью {doctor.Role} не может быть привязан как врач.");

        var linkExists = await _doctorChildLinks.AnyAsync(
            l => l.DoctorUserId == doctorUserId && l.ChildId == childId, cancellationToken);

        if (linkExists)
            throw new InvalidOperationException(
                $"Связь врач {doctorUserId} — ребёнок {childId} уже существует.");

        _doctorChildLinks.Add(new DoctorChildLink
        {
            DoctorUserId = doctorUserId,
            ChildId = childId,
            CreatedAt = DateTime.UtcNow
        });

        await _doctorChildLinks.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "admin.doctorchildlinkcreated",
            targetType: "DoctorChildLink",
            targetId: $"{doctorUserId}:{childId}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Создана связь врач {DoctorId} — ребёнок {ChildId}.",
            doctorUserId, childId);
    }

    public async Task<bool> RemoveDoctorChildLinkAsync(
        Guid doctorUserId,
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        var link = await _doctorChildLinks.FirstOrDefaultAsync(
            l => l.DoctorUserId == doctorUserId && l.ChildId == childId,
            cancellationToken);

        if (link is null)
        {
            _logger.LogWarning(
                "RemoveDoctorChildLink: связь {DoctorId}:{ChildId} не найдена.",
                doctorUserId, childId);
            return false;
        }

        _doctorChildLinks.Remove(link);
        await _doctorChildLinks.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "admin.doctorchildlinkdeleted",
            targetType: "DoctorChildLink",
            targetId: $"{doctorUserId}:{childId}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Удалена связь врач {DoctorId} — ребёнок {ChildId}.",
            doctorUserId, childId);

        return true;
    }

    public async Task<List<AdminParentChildLinkResponse>> GetAllParentChildLinksAsync(
        CancellationToken cancellationToken = default)
    {
        var query =
            from link in _parentChildLinks.Query()
            join user in _users.Query() on link.ParentUserId equals user.UserId into users
            from user in users.DefaultIfEmpty()
            join child in _children.Query() on link.ChildId equals child.ChildId into children
            from child in children.DefaultIfEmpty()
            orderby link.CreatedAt descending
            select new AdminParentChildLinkResponse
            {
                ParentUserId = link.ParentUserId,
                ParentDisplayName = user != null ? user.EmailForLogin : null,
                ChildId = link.ChildId,
                ChildDisplayName = child != null
                    ? $"{child.FirstName} {child.LastName}".Trim()
                    : null,
                LinkedAt = link.CreatedAt,
                CreatedBy = link.LinkedByUserId == null
                    ? null
                    : link.LinkedByUserId.ToString()
            };

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<AdminDoctorChildLinkResponse>> GetAllDoctorChildLinksAsync(
        CancellationToken cancellationToken = default)
    {
        var query =
            from link in _doctorChildLinks.Query()
            join user in _users.Query() on link.DoctorUserId equals user.UserId into users
            from user in users.DefaultIfEmpty()
            join child in _children.Query() on link.ChildId equals child.ChildId into children
            from child in children.DefaultIfEmpty()
            orderby link.CreatedAt descending
            select new AdminDoctorChildLinkResponse
            {
                DoctorUserId = link.DoctorUserId,
                DoctorDisplayName = user != null ? user.EmailForLogin : null,
                ChildId = link.ChildId,
                ChildDisplayName = child != null
                    ? $"{child.FirstName} {child.LastName}".Trim()
                    : null,
                LinkedAt = link.CreatedAt,
                CreatedBy = link.LinkedByUserId == null
                    ? null
                    : link.LinkedByUserId.ToString()
            };

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<AdminSystemStatsResponse> GetSystemStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var hangfireActiveJobs = (int)(monitoring.EnqueuedCount("default")
                                     + monitoring.ProcessingCount());

        // Repositories in this service share the same scoped DbContext. EF Core
        // forbids parallel operations on one context instance, so keep these
        // inexpensive aggregate queries sequential.
        var totalUsers = await _users.CountAsync(cancellationToken: cancellationToken);
        var totalChildren = await _children.CountAsync(cancellationToken: cancellationToken);
        var totalMeasurements = await _measurements.LongCountAsync(cancellationToken: cancellationToken);
        var pendingSyncItems = await _syncLogs.CountByStatusAsync("pending", cancellationToken);
        var unresolvedConflicts = await _syncLogs.CountUnresolvedConflictsAsync(cancellationToken);
        var pendingExportJobs = await _exportJobs.CountByStatusAsync("queued", cancellationToken);
        var completedToday = await _exportJobs.CountCompletedTodayAsync(cancellationToken);

        _logger.LogInformation(
            "SystemStats: Users={Users}, Children={Children}, Measurements={Measurements}, Hangfire={Hangfire}.",
            totalUsers, totalChildren, totalMeasurements, hangfireActiveJobs);

        return new AdminSystemStatsResponse
        {
            HangfireActiveJobs = hangfireActiveJobs,
            TotalUsers = totalUsers,
            TotalChildren = totalChildren,
            TotalMeasurements = totalMeasurements,
            PendingSyncItems = pendingSyncItems,
            UnresolvedConflicts = unresolvedConflicts,
            PendingExportJobs = pendingExportJobs,
            CompletedExportJobsToday = completedToday,
            ServerUtcTime = DateTime.UtcNow
        };
    }

    public async Task<List<InviteCodeResponse>> GetInvitationsAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _inviteCodes.Query();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(c => c.Status == normalizedStatus);
        }

        var now = DateTime.UtcNow;

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new InviteCodeResponse
            {
                InviteCodeId = c.InviteCodeId,
                ChildId = c.ChildId,
                Code = c.Code,
                TargetRole = c.TargetRole,
                Status = c.Status,
                ExpiresAt = c.ExpiresAt,
                CreatedAt = c.CreatedAt,
                IsActive = c.Status == "Pending" && c.ExpiresAt > now
            })
            .ToListAsync(cancellationToken);
    }

    public Task<InviteCodeResponse> CreateInvitationAsync(
        Guid childId,
        UserRole targetRole,
        CancellationToken cancellationToken = default)
    {
        if (targetRole != UserRole.Parent && targetRole != UserRole.Doctor)
            throw new ArgumentException("TargetRole должен быть Parent или Doctor.");

        return _inviteCodeService.GenerateAsync(childId, targetRole, cancellationToken);
    }

    public async Task<bool?> RevokeInvitationAsync(
        Guid inviteCodeId,
        CancellationToken cancellationToken = default)
    {
        var invite = await _inviteCodes.FirstOrDefaultAsync(
            c => c.InviteCodeId == inviteCodeId, cancellationToken);

        if (invite is null)
            return null;

        if (invite.Status != "Pending")
            throw new InvalidOperationException(
                $"Инвайт-код уже использован или недоступен для отзыва (статус: {invite.Status}).");

        var revoked = await _inviteCodeService.RevokeAsync(inviteCodeId, cancellationToken);
        return revoked ? true : null;
    }

    public async Task<List<AuditLogResponse>> GetAuditLogsAsync(
        Guid? actorUserId,
        string? action,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Параметр 'from' не может быть позже 'to'.");

        var safeLimit = Math.Clamp(limit, 1, 1000);

        var query = BuildAuditLogsQuery(actorUserId, action, from, to);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(safeLimit)
            .Select(a => new AuditLogResponse
            {
                AuditLogId = a.AuditLogId,
                ActorUserId = a.ActorUserId,
                Action = a.Action,
                TargetType = a.TargetType,
                TargetId = a.TargetId,
                Details = a.Details,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditLogResponse>> GetAuditLogsPageAsync(
        Guid? actorUserId,
        string? action,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("Параметр 'from' не может быть позже 'to'.");
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 10, 100);
        var query = BuildAuditLogsQuery(actorUserId, action, from, to);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.AuditLogId)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(item => new AuditLogResponse
            {
                AuditLogId = item.AuditLogId,
                ActorUserId = item.ActorUserId,
                Action = item.Action,
                TargetType = item.TargetType,
                TargetId = item.TargetId,
                Details = item.Details,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    private IQueryable<AuditLog> BuildAuditLogsQuery(
        Guid? actorUserId,
        string? action,
        DateTime? from,
        DateTime? to)
    {
        var query = _auditLogs.Query().AsNoTracking();

        if (actorUserId.HasValue)
        {
            query = query.Where(item => item.ActorUserId == actorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionFilter = action.Trim();
            query = query.Where(item => EF.Functions.ILike(item.Action, $"%{actionFilter}%"));
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.CreatedAt <= to.Value);
        }

        return query;
    }

    public async Task<OnboardingFunnelResponse> GetOnboardingFunnelAsync(
        string? role,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Параметр 'from' не может быть позже 'to'.");

        UserRole? parsedRole = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role.Trim(), ignoreCase: true, out var roleValue))
                throw new ArgumentException($"Неизвестная роль '{role}'. Допустимые значения: Parent, Doctor, Admin, SupportAdmin.");
            parsedRole = roleValue;
        }

        var eventsQuery = _onboardingEvents.Query();

        if (parsedRole.HasValue)
        {
            var roleName = parsedRole.Value.ToString();
            eventsQuery = eventsQuery.Where(e => e.UserRole == roleName);
        }

        if (from.HasValue)
            eventsQuery = eventsQuery.Where(e => e.CreatedAt >= from.Value);

        if (to.HasValue)
            eventsQuery = eventsQuery.Where(e => e.CreatedAt <= to.Value);

        var funnelEvents = await eventsQuery
            .Where(e => e.EventType == "started" || e.EventType == "completed")
            .Select(e => new { e.StepNumber, e.UserId, e.EventType })
            .ToListAsync(cancellationToken);

        var stepCounts = funnelEvents
            .GroupBy(e => e.StepNumber)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                StepNumber = g.Key,
                UniqueUsers = g.Select(x => x.UserId).Distinct().Count()
            })
            .ToList();

        var totalStarted = funnelEvents
            .Where(e => e.StepNumber == 1)
            .Select(e => e.UserId)
            .Distinct()
            .Count();

        if (totalStarted == 0)
        {
            totalStarted = funnelEvents
                .Select(e => e.UserId)
                .Distinct()
                .Count();
        }

        var usersQuery = _users.Query().Where(u => u.OnboardingCompleted);

        if (parsedRole.HasValue)
            usersQuery = usersQuery.Where(u => u.Role == parsedRole.Value);

        if (from.HasValue)
            usersQuery = usersQuery.Where(u =>
                u.OnboardingCompletedAt >= from.Value || u.OnboardingSkippedAt >= from.Value);

        if (to.HasValue)
            usersQuery = usersQuery.Where(u =>
                u.OnboardingCompletedAt <= to.Value || u.OnboardingSkippedAt <= to.Value);

        var totalCompleted = await usersQuery.CountAsync(cancellationToken);

        if (totalCompleted == 0)
        {
            totalCompleted = funnelEvents
                .Where(e => e.EventType == "completed")
                .Select(e => e.UserId)
                .Distinct()
                .Count();
        }

        var baselineUsers = stepCounts.FirstOrDefault()?.UniqueUsers ?? 0;

        var steps = stepCounts
            .Select(s => new OnboardingFunnelStepDto
            {
                StepNumber = s.StepNumber,
                UniqueUsers = s.UniqueUsers,
                RetentionRate = baselineUsers > 0
                    ? Math.Round((double)s.UniqueUsers / baselineUsers, 4)
                    : 0
            })
            .ToList();

        var conversionRate = totalStarted > 0
            ? Math.Round((double)totalCompleted / totalStarted, 4)
            : 0;

        return new OnboardingFunnelResponse
        {
            FilterRole = parsedRole?.ToString(),
            FilterFrom = from,
            FilterTo = to,
            TotalStarted = totalStarted,
            TotalCompleted = totalCompleted,
            ConversionRate = conversionRate,
            Steps = steps
        };
    }
}
