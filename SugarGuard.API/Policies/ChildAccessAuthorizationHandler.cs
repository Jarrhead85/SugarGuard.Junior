using Microsoft.AspNetCore.Authorization;
using SugarGuard.API.Services;

namespace SugarGuard.API.Policies;

/// <summary>
/// Проверяет доступ к ребёнку как resource-based правило авторизации.
/// </summary>
public sealed class ChildAccessAuthorizationHandler : AuthorizationHandler<ChildAccessRequirement, Guid>
{
    private readonly IChildAccessService _childAccessService;

    public ChildAccessAuthorizationHandler(IChildAccessService childAccessService)
    {
        _childAccessService = childAccessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ChildAccessRequirement requirement,
        Guid childId)
    {
        if (childId != Guid.Empty
            && await _childAccessService.CanAccessChildAsync(childId, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
