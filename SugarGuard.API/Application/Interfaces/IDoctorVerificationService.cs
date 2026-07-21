using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Управляет заявками кандидатов в врачи и закрытыми документами квалификации.
/// </summary>
public interface IDoctorVerificationService
{
    Task<DoctorVerificationResponse?> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DoctorVerificationResponse> SubmitAsync(Guid userId, SubmitDoctorVerificationRequest request, IReadOnlyCollection<IFormFile> documents, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminDoctorVerificationResponse>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<AdminDoctorVerificationResponse?> ApproveAsync(Guid requestId, Guid reviewerUserId, string? comment, CancellationToken cancellationToken = default);
    Task<AdminDoctorVerificationResponse?> RejectAsync(Guid requestId, Guid reviewerUserId, string comment, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType, string FileName)?> OpenDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
