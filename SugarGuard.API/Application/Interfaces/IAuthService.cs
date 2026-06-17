using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика аутентификации
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Проверяет учётные данные пользователя и возвращает данные для JWT-выпуска
    /// </summary>
    Task<LoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthRegistrationResult> RegisterParentAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthEmailVerificationResult> ConfirmEmailAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает пользователя по его ID и проверяет активность
    /// </summary>
    Task<(User? User, bool IsActive)> GetUserForRefreshAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает аудит об успешной ротации refresh-токена
    /// </summary>
    Task WriteRefreshSuccessAuditAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает аудит о невалидной попытке ротации refresh-токена
    /// </summary>
    Task WriteRefreshFailedAuditAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Находит пользователя по email (для ForgotPassword)
    /// </summary>
    Task<User?> FindActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает аудит о запросе сброса пароля
    /// </summary>
    Task WriteForgotPasswordAuditAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет код сброса и устанавливает новый пароль
    /// </summary>
    Task<ResetPasswordResult> ResetPasswordAsync(
        string email,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ServiceAccount пользователя для бота, создавая запись при первом обращении
    /// </summary>
    Task<User> GetOrCreateServiceAccountAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет API-ключ бота: сопоставляет с переменной окружения
    /// </returns>
    bool? ValidateBotApiKey(string? providedKey);

    /// <summary>
    /// Записывает аудит о входе бота (успех/провал)
    /// </summary>
    Task WriteBotLoginAuditAsync(
        bool success,
        string? serviceAccountUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает аудит о выходе пользователя
    /// </summary>
    Task WriteLogoutAuditAsync(
        string? userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат операции входа
/// </summary>
public sealed record LoginResult(
    User? User,
    LoginFailureReason FailureReason);

public sealed record AuthRegistrationResult(
    User? User,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record AuthEmailVerificationResult(
    User? User,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// Причина отказа при попытке входа
/// </summary>
public enum LoginFailureReason
{
    None,
    UserNotFound,
    AccountDeactivated,
    PasswordNotConfigured,
    PasswordMismatch,
    EmailNotVerified
}

/// <summary>
/// Результат операции сброса пароля
/// /summary>
public sealed record ResetPasswordResult(
    User? User,
    bool Success,
    string? ErrorMessage);
