using SugarGuard.API.Application.Services;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Сервис верификации контактных данных пользователя
    /// </summary>
    public interface IVerificationService
    {
        /// <summary>
        /// Отправляет одноразовый код на указанный email.
        /// </summary>
        Task<SendVerificationCodeResult> SendCodeAsync(
            string email,
            VerificationPurpose purpose,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Проверяет введённый пользователем код
        /// </summary>

        Task<VerifyCodeResult> VerifyCodeAsync(
            string email,
            string code,
            VerificationPurpose purpose,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Проверяет, подтверждён ли email
        /// </summary>
        bool IsEmailVerified(
            string email,
            string verificationToken,
            VerificationPurpose purpose);
    }
}
