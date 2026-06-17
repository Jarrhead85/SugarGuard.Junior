using SugarGuard.Application.Security;
using SugarGuard.Domain.Enums;
using SugarGuard.Domain.Security;

namespace SugarGuard.Infrastructure.Security;

public class RolePermissionService : IRolePermissionService
{
    private static readonly IReadOnlyCollection<string> ParentPermissions =
    [
        Permission.ChildRead,
        Permission.ChildWrite,
        Permission.DashboardRead,
        Permission.ExportRead,
        Permission.ExportWrite,
        Permission.FaqRead
    ];

    private static readonly IReadOnlyCollection<string> DoctorPermissions =
    [
        Permission.ChildRead,
        Permission.DashboardRead,
        Permission.FaqRead
    ];

    private static readonly IReadOnlyCollection<string> AdminPermissions =
    [
        Permission.ChildRead,
        Permission.ChildWrite,
        Permission.DashboardRead,
        Permission.ExportRead,
        Permission.ExportWrite,
        Permission.SyncRead,
        Permission.FaqRead,
        Permission.FaqWrite,
        Permission.AdminUsersRead,
        Permission.AdminUsersWrite,
        Permission.AdminRolesWrite,
        Permission.AuditRead
    ];

    private static readonly IReadOnlyCollection<string> ServiceAccountPermissions =
    [
        Permission.ChildRead,
        Permission.ChildWrite,
        Permission.DashboardRead,
        Permission.ExportRead,
        Permission.SyncRead,
        Permission.FaqRead
    ];

    public IReadOnlyCollection<string> GetPermissions(UserRole role) => role switch
    {
        UserRole.Parent => ParentPermissions,
        UserRole.Doctor => DoctorPermissions,
        UserRole.Admin => AdminPermissions,
        UserRole.SupportAdmin => AdminPermissions,
        UserRole.ServiceAccount => ServiceAccountPermissions,
        _ => []
    };
}
