using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Web.Services;

/// <summary>
/// Предоставляет информацию о текущем аутентифицированном пользователе
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<CurrentUserService> _logger;

    /// <summary>
    /// Инициализирует сервис через DI
    /// </summary>
    public CurrentUserService(
        AuthenticationStateProvider authStateProvider,
        ILogger<CurrentUserService> logger)
    {
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    // Основные свойства
    /// <inheritdoc />
    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);
        return principal.Identity?.IsAuthenticated == true;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);

        var raw = principal.FindFirstValue("UserId")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (Guid.TryParse(raw, out var userId))
            return userId;

        _logger.LogWarning(
            "CurrentUserService: claim UserId содержит невалидный Guid: {Raw}", raw);
        return null;
    }

    /// <inheritdoc />
    public async Task<string?> GetRoleAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);

        return principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");
    }

    /// <inheritdoc />
    public async Task<string?> GetEmailAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);
        return principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");
    }

    /// <inheritdoc />
    public async Task<long?> GetTelegramIdAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);
        var raw = principal.FindFirstValue("TelegramId");

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (long.TryParse(raw, out var telegramId))
            return telegramId;

        _logger.LogWarning(
            "CurrentUserService: claim TelegramId содержит невалидное значение: {Raw}", raw);
        return null;
    }

    // Проверки ролей
    /// <inheritdoc />
    public async Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);
        return principal.IsInRole(role);
    }

    /// <inheritdoc />
    public async Task<bool> IsParentAsync(CancellationToken cancellationToken = default)
        => await IsInRoleAsync(Roles.Parent, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsDoctorAsync(CancellationToken cancellationToken = default)
        => await IsInRoleAsync(Roles.Doctor, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsAdminAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);
        return principal.IsInRole(Roles.Admin)
            || principal.IsInRole(Roles.SupportAdmin);
    }

    // Проверки разрешений
    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return false;

        var principal = await GetPrincipalAsync(cancellationToken);

        return principal.HasClaim(
            c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase)
                 && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);

        var permissions = principal.Claims
            .Where(c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        return permissions;
    }

    // Снэпшот состояния пользователя
    /// <inheritdoc />
    public async Task<CurrentUserSnapshot?> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);

        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var rawUserId = principal.FindFirstValue("UserId")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            _logger.LogWarning(
                "CurrentUserService.GetSnapshotAsync: невалидный UserId в claims.");
            return null;
        }

        var role = principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role")
            ?? string.Empty;

        var permissions = principal.Claims
            .Where(c => c.Type == c.Value)
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");

        var rawTelegramId = principal.FindFirstValue("TelegramId");
        long? telegramId = long.TryParse(rawTelegramId, out var tg) ? tg : null;

        return new CurrentUserSnapshot(
            UserId: userId,
            Role: role,
            Email: email,
            TelegramId: telegramId,
            Permissions: permissions);
    }

    // Вспомогательный метод
    /// <summary>
    private async Task<ClaimsPrincipal> GetPrincipalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var authState = await _authStateProvider
                .GetAuthenticationStateAsync()
                .ConfigureAwait(false);

            return authState.User;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CurrentUserService: не удалось получить AuthenticationState.");
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
