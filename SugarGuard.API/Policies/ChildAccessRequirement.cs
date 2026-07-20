using Microsoft.AspNetCore.Authorization;

namespace SugarGuard.API.Policies;

/// <summary>
/// Требует доступа текущего пользователя к данным конкретного ребёнка.
/// </summary>
public sealed class ChildAccessRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "ChildAccess";
}
