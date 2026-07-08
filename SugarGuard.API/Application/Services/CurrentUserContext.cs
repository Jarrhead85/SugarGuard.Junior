using System.Security.Claims;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetUserId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal?.FindFirstValue("UserId");

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    public UserRole? GetRole()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.Role)
                    ?? principal?.FindFirstValue("role");

        return Enum.TryParse<UserRole>(value, ignoreCase: true, out var role) ? role : null;
    }
}
