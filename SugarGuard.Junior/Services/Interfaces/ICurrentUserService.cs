namespace SugarGuard.Junior.Services.Interfaces;

public interface ICurrentUserService
{
    /// <summary>
    /// Возвращает userId текущего авторизованного пользователя из SecureStorage.
    /// </summary>
    Task<string?> GetCurrentUserIdAsync();
}
