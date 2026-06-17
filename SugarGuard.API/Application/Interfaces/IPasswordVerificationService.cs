namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Проверка пароля
/// </summary>
public interface IPasswordVerificationService
{
    bool VerifyPassword(string password, string hashBase64, string saltBase64); // Проверяет пароль
}
