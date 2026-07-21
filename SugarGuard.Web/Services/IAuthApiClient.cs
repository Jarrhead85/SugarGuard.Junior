using SugarGuard.Shared.Dto;
using SugarGuard.Web.Models.Auth;
using SugarGuard.Web.Services;
using SugarGuard.Web.ViewModels;

namespace SugarGuard.Web.Services;

/// <summary>
/// HTTP-клиент для взаимодействия с эндпоинтами аутентификации
/// </summary>
public interface IAuthApiClient
{
    /// <summary>
    /// Выполняет вход по email и паролю
    /// </summary>
    Task<LoginResult> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Регистрирует нового пользователя как родителя или кандидата в врачи.
    /// </summary>
    Task<RegisterResult> RegisterAsync(string email, string password, string role = "Parent",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет 8-значный код подтверждения email
    /// </summary>
    Task<VerifyEmailResult> VerifyEmailAsync(string email, string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Повторно отправляет письмо с кодом подтверждения
    /// </summary>
    Task<bool> SendEmailVerificationCodeAsync(string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет выход: отзывает refresh-токен на сервере
    /// </summary>
    Task LogoutAsync(string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет код сброса пароля на указанный email
    /// </summary>
    Task<bool> SendPasswordResetCodeAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Устанавливает новый пароль, проверяя одноразовый код сброс
    /// </summary>
    Task<ResetPasswordResult> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает статус онбординга текущего пользователя
    /// </summary>
    Task<OnboardingStatusResponse> GetOnboardingStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Подтверждает email-код верификации
    /// </summary>
    Task<VerifyEmailResponse> VerifyEmailCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Повторно отправляет письмо с кодом подтверждения
    /// </summary>
    Task<bool> ResendEmailVerificationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт профиль ребёнка в рамках онбординга
    /// </summary>
    Task<CreateChildOnboardingResponse> CreateChildOnboardingAsync(CreateChildOnboardingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершает конкретный шаг онбординга
    /// </summary>
    Task<OnboardingStatusResponse> CompleteOnboardingStepAsync(int step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершает весь онбординг
    /// </summary>
    Task<CompleteOnboardingResponse> CompleteOnboardingAsync(CompleteOnboardingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Пропускает онбординг
    /// </summary>
    Task SkipOnboardingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ChildId текущего пользователя
    /// </summary>
    Task<Guid> GetCurrentChildIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет настройки диабета ребёнка
    /// </summary>
    Task UpdateDiabetesSettingsAsync(
        Guid childId,
        UpdateDiabetesSettingsRequest request,
        CancellationToken cancellationToken = default);
}
