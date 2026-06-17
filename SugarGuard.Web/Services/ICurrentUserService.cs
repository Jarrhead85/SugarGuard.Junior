namespace SugarGuard.Web.Services;

/// <summary>
/// Предоставляет информацию о текущем пользователе Web-приложения
/// </summary>
public interface ICurrentUserService
{
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default); // Возвращает true, если пользователь аутентифицирован

    Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken = default); // Возвращает Guid текущего пользователя

    Task<string?> GetRoleAsync(CancellationToken cancellationToken = default); // Возвращает системное название роли 

    Task<string?> GetEmailAsync(CancellationToken cancellationToken = default); // Возвращает email пользователя

    Task<long?> GetTelegramIdAsync(CancellationToken cancellationToken = default); // Возвращает Telegram ID пользователя   

    Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default); // Возвращает true, если пользователь имеет указанную роль
       
    Task<bool> IsParentAsync(CancellationToken cancellationToken = default); // Возвращает true, если роль пользователя — Parent
       
    Task<bool> IsDoctorAsync(CancellationToken cancellationToken = default); // Возвращает true, если роль пользователя — Doctor

    Task<bool> IsAdminAsync(CancellationToken cancellationToken = default); // Возвращает true, если роль пользователя — Admin

    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default); // Возвращает true, если JWT-токен содержит указанное разрешение

    Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default); // Возвращает все разрешения текущего пользователя

    Task<CurrentUserSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default); // Возвращает снэпшот всех данных о пользователе одним вызовом
}

/// <summary>
/// Иммутабельный снэпшот данных текущего пользователя
/// </summary>
public sealed record CurrentUserSnapshot(
    Guid UserId,
    string Role,
    string? Email,
    long? TelegramId,
    IReadOnlyList<string> Permissions);
