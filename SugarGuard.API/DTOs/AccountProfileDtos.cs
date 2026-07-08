using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

public sealed class AccountProfileResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
}

public sealed class UpdateAccountProfileRequest
{
    [Required, StringLength(80)]
    public string FirstName { get; init; } = string.Empty;

    [StringLength(80)]
    public string LastName { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Specialty { get; init; }

    [StringLength(120)]
    public string? LicenseNumber { get; init; }
}

public sealed class AccountPhotoUploadResponse
{
    public string PhotoUrl { get; init; } = string.Empty;
}
