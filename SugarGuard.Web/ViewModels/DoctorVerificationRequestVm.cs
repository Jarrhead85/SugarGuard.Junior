namespace SugarGuard.Web.ViewModels;

/// <summary>
/// Заявка кандидата в врачи, доступная полному администратору.
/// </summary>
public sealed class DoctorVerificationRequestVm
{
    public Guid RequestId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Specialty { get; init; } = string.Empty;
    public string LicenseNumber { get; init; } = string.Empty;
    public string? OrganizationName { get; init; }
    public string? Comment { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewComment { get; init; }
    public IReadOnlyList<DoctorVerificationDocumentVm> Documents { get; init; } = [];
}

public sealed class DoctorVerificationDocumentVm
{
    public Guid DocumentId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
}
