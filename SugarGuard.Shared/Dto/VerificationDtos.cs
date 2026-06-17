using System.ComponentModel.DataAnnotations;
using SugarGuard.Shared.Validation;

namespace SugarGuard.Shared.Dto;

// Подтверждение email
/// <summary>
/// Запрос на отправку кода подтверждения email
/// </summary>
public sealed class SendEmailVerificationRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Ответ на запрос отправки кода подтверждения email
/// </summary>
public sealed class SendEmailVerificationResponse
{
    public bool Success { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? MaskedEmail { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Запрос на подтверждение email с помощью кода из письма
/// </summary>
public sealed class VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Код из письма в формате ABCD-1234.
    /// </summary>
    [Required]
    [StringLength(9, MinimumLength = 8)]
    [ConnectionCode]
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Ответ на подтверждение email
/// </summary>
public sealed class VerifyEmailResponse
{
    public bool IsValid { get; init; }

    public string? Message { get; init; }

    public string? ErrorMessage { get; init; }
}

// Подтверждение номера телефона (SMS)
/// <summary>
/// Запрос на отправку SMS-кода подтверждения номера телефона
/// </summary>
public sealed class SendPhoneVerificationRequest
{
    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;
}

/// <summary>
/// Ответ на запрос отправки SMS-кода подтверждения
/// </summary>
public sealed class SendPhoneVerificationResponse
{
    public bool Success { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public string? MaskedPhone { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Запрос на подтверждение номера телефона с помощью 8-значного SMS-кода.
/// </summary>
public sealed class VerifyPhoneRequest
{
    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required]
    [StringLength(9, MinimumLength = 8)]
    [ConnectionCode]
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Ответ на подтверждение номера телефона
/// </summary>
public sealed class VerifyPhoneResponse
{
    public bool IsValid { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }
}

// OAuth через Яндекс
/// <summary>
/// Запрос на вход / регистрацию через OAuth Яндекс
/// </summary>
public sealed class YandexOAuthRequest
{
    [Required]
    [MaxLength(512)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string RedirectUri { get; init; } = string.Empty;

    [MaxLength(32)]
    public string? TargetRole { get; init; }
}

/// <summary>
/// Ответ на вход / регистрацию через OAuth Яндекс
/// </summary>
public sealed class YandexOAuthResponse
{
    public bool Success { get; init; }
    public Guid? UserId { get; init; }
    public string? Role { get; init; }
    public IReadOnlyList<string>? Permissions { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsNewUser { get; init; }
    public string? ErrorMessage { get; init; }
}

// Статус верификации пользователя
/// <summary>
/// Сводная информация о статусе верификации пользователя
public sealed class VerificationStatusResponse
{
    public bool IsEmailVerified { get; init; }
    public bool IsPhoneVerified { get; init; }
    public DateTime? EmailVerifiedAt { get; init; }
    public DateTime? PhoneVerifiedAt { get; init; }
    public string? MaskedEmail { get; init; }
    public string? MaskedPhone { get; init; }
}

// Типы кодов верификации
/// <summary>
/// Строковые константы типов кодов верификации
/// </summary>
public static class VerificationCodeType
{
    public const string EmailVerify = "EmailVerify";
    public const string PhoneVerify = "PhoneVerify";
}
