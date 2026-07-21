using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

public sealed class SubmitDoctorVerificationRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Specialty { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 3)]
    public string LicenseNumber { get; init; } = string.Empty;

    [StringLength(240)]
    public string? OrganizationName { get; init; }

    [StringLength(2000)]
    public string? Comment { get; init; }
}

public sealed class ReviewDoctorVerificationRequest
{
    [StringLength(1000)]
    public string? Comment { get; init; }
}

public sealed class DoctorVerificationDocumentResponse
{
    public Guid DocumentId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
}

public class DoctorVerificationResponse
{
    public Guid RequestId { get; init; }
    public Guid UserId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Specialty { get; init; } = string.Empty;
    public string LicenseNumber { get; init; } = string.Empty;
    public string? OrganizationName { get; init; }
    public string? Comment { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewComment { get; init; }
    public IReadOnlyList<DoctorVerificationDocumentResponse> Documents { get; init; } = [];
}

public sealed class AdminDoctorVerificationResponse : DoctorVerificationResponse
{
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}
