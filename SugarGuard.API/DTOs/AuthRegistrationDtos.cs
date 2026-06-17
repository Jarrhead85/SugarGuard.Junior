using System.ComponentModel.DataAnnotations;
using SugarGuard.Shared.Validation;

namespace SugarGuard.API.DTOs;

public sealed class RegisterUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(9, MinimumLength = 8)]
    [ConnectionCode]
    public string Code { get; init; } = string.Empty;
}

public sealed class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}
