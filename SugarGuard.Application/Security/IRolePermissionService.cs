using SugarGuard.Domain.Enums;

namespace SugarGuard.Application.Security;

public interface IRolePermissionService
{
    IReadOnlyCollection<string> GetPermissions(UserRole role);
}
