using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Фильтр авторизации для Hangfire Dashboard
/// </summary>

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Роли с доступом к Hangfire Dashboard
    /// </summary>
    private static readonly HashSet<UserRole> _allowedRoles = new()
    {
        UserRole.Admin,
        UserRole.SupportAdmin
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HangfireAuthorizationFilter> _logger;

    public HangfireAuthorizationFilter(
        IHttpContextAccessor httpContextAccessor,
        ILogger<HangfireAuthorizationFilter> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public bool Authorize(DashboardContext dashboardContext)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return false;

        if (httpContext.User.Identity?.IsAuthenticated != true)
            return false;

        if (!httpContext.User.IsInRole(UserRole.Admin.ToString())
            && !httpContext.User.IsInRole(UserRole.SupportAdmin.ToString()))
        {
            _logger.LogWarning(
                "HangfireDashboard: отказ в доступе. UserId={UserId} IP={Ip} Path={Path} " +
                "Roles={Roles}.",
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                httpContext.Connection.RemoteIpAddress,
                httpContext.Request.Path,
                string.Join(',',
                    httpContext.User.FindAll(System.Security.Claims.ClaimTypes.Role)
                        .Select(c => c.Value)));
            return false;
        }

        return true;
    }
}
