using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services;

/// <summary>
/// Provides the authenticated user identity for the current HTTP request.
/// </summary>
public interface ICurrentUserContext
{
    Guid? GetUserId();
    UserRole? GetRole();
}
