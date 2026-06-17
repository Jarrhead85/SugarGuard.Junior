// Интерфейс для аутентификации пользователей
namespace SugarGuard.Junior.Services.Interfaces;

using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;

public interface IAuthenticationService
{
    /// <summary>
    /// Проверяет, авторизирован ли текущий пользователь
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Получает текущего авторизированного пользователя
    /// Возвращает null если не авторизирован
    /// </summary>
    Task<User?> GetCurrentUserAsync();

    /// <summary>
    /// Регистрирует нового пользователя
    /// Выбрасывает исключение если данные невалидны или пользователь уже существует
    /// </summary>
    Task<User> RegisterAsync(string firstName, string lastName, string email, string phoneNumber, string password);

    /// <summary>
    /// Входит в аккаунт
    /// Возвращает true если успешно, false если неверные учётные данные
    /// </summary>
    Task<bool> LoginAsync(string email, string password);

    /// <summary>
    /// Выходит из аккаунта
    /// </summary>
    Task<bool> LogoutAsync();

    /// <summary>
    /// Проверяет, верифицирован ли email пользователя
    /// </summary>
    Task<bool> IsEmailVerifiedAsync();

    /// <summary>
    /// Подтверждает email с помощью кода
    /// </summary>
    Task<VerifyCodeResponse> VerifyEmailAsync(string email, string verificationCode);

    /// <summary>
    /// Отправляет код подтверждения на email
    /// </summary>
    Task<bool> SendEmailVerificationCodeAsync(string email);

    Task<bool> RefreshTokenAsync();
}
