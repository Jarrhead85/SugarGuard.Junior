using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SugarGuard.API.Policies;

/// <summary>
/// Автоматически применяет ресурсную авторизацию ко всем action с параметром childId.
/// Это исключает появление нового endpoint с данными ребёнка без проверки доступа.
/// </summary>
public sealed class ChildAccessAuthorizationFilter : IAsyncActionFilter
{
    private readonly IAuthorizationService _authorizationService;

    public ChildAccessAuthorizationFilter(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!TryGetChildId(context, out var childId)
            || (context.ActionDescriptor.EndpointMetadata?.OfType<IAllowAnonymous>().Any() ?? false))
        {
            await next();
            return;
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            context.HttpContext.User,
            childId,
            ChildAccessRequirement.PolicyName);

        if (!authorizationResult.Succeeded)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private static bool TryGetChildId(ActionExecutingContext context, out Guid childId)
    {
        childId = Guid.Empty;

        if (!context.ActionArguments.TryGetValue("childId", out var value) || value is null)
        {
            return false;
        }

        switch (value)
        {
            case Guid id when id != Guid.Empty:
                childId = id;
                return true;
            default:
                return false;
        }
    }
}
