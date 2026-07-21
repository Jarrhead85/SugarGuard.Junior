using System.Net;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Security;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Безопасная обработка заявок на роль врача.
/// </summary>
public sealed class DoctorVerificationService : IDoctorVerificationService
{
    private const int MaxDocuments = 3;
    private const long MaxDocumentBytes = 10L * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png"
    };

    private readonly AppDbContext _db;
    private readonly ICryptoService _crypto;
    private readonly IUploadPathProvider _uploadPaths;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ILogger<DoctorVerificationService> _logger;

    public DoctorVerificationService(AppDbContext db, ICryptoService crypto, IUploadPathProvider uploadPaths, IAuditService audit, IEmailService email, ILogger<DoctorVerificationService> logger)
    {
        _db = db;
        _crypto = crypto;
        _uploadPaths = uploadPaths;
        _audit = audit;
        _email = email;
        _logger = logger;
    }

    public async Task<DoctorVerificationResponse?> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var request = await Query().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return request is null ? null : Map(request);
    }

    public async Task<DoctorVerificationResponse> SubmitAsync(Guid userId, SubmitDoctorVerificationRequest request, IReadOnlyCollection<IFormFile> documents, CancellationToken cancellationToken = default)
    {
        if (documents.Count is < 1 or > MaxDocuments)
            throw new ArgumentException("Приложите от одного до трёх документов.");

        var user = await _db.Users.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Пользователь не найден.");
        if (user.Role != UserRole.DoctorPending)
            throw new InvalidOperationException("Заявку может отправить только кандидат в врачи.");

        var current = await _db.DoctorVerificationRequests
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (current?.Status == DoctorVerificationStatus.Submitted)
            throw new InvalidOperationException("Заявка уже отправлена и ожидает проверки.");

        var preparedFiles = new List<PreparedFile>(documents.Count);
        foreach (var document in documents)
            preparedFiles.Add(await ValidateAndReadAsync(document, cancellationToken));

        current ??= new DoctorVerificationRequest { UserId = userId };
        current.Specialty = request.Specialty.Trim();
        current.EncryptedLicenseNumber = _crypto.Encrypt(request.LicenseNumber.Trim());
        current.OrganizationName = Normalize(request.OrganizationName);
        current.Comment = Normalize(request.Comment);
        current.Status = DoctorVerificationStatus.Submitted;
        current.SubmittedAt = DateTime.UtcNow;
        current.ReviewedAt = null;
        current.ReviewedByUserId = null;
        current.ReviewComment = null;

        Directory.CreateDirectory(_uploadPaths.DoctorVerificationDirectory);
        foreach (var oldDocument in current.Documents)
            DeleteStoredFile(oldDocument.StoredFileName);
        _db.DoctorVerificationDocuments.RemoveRange(current.Documents);
        current.Documents.Clear();

        foreach (var file in preparedFiles)
        {
            var storedFileName = $"{Guid.NewGuid():N}{file.Extension}";
            var path = _uploadPaths.GetDoctorVerificationFilePath(storedFileName);
            await File.WriteAllBytesAsync(path, file.Content, cancellationToken);
            current.Documents.Add(new DoctorVerificationDocument
            {
                OriginalFileName = file.OriginalFileName,
                ContentType = file.ContentType,
                StoredFileName = storedFileName,
                SizeBytes = file.Content.LongLength,
                UploadedAt = DateTime.UtcNow
            });
        }

        user.DoctorSpecialty = current.Specialty;
        user.EncryptedDoctorLicense = current.EncryptedLicenseNumber;
        if (current.RequestId == Guid.Empty)
            _db.DoctorVerificationRequests.Add(current);

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("doctor_verification.submitted", "DoctorVerificationRequest", current.RequestId.ToString("D"), $"documents={current.Documents.Count}", cancellationToken);
        await NotifyAdministratorsAsync(user, current, cancellationToken);
        return Map(current);
    }

    public async Task<IReadOnlyList<AdminDoctorVerificationResponse>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var requests = await Query()
            .Where(item => item.Status == DoctorVerificationStatus.Submitted)
            .OrderBy(item => item.SubmittedAt)
            .ToListAsync(cancellationToken);
        return requests.Select(MapAdmin).ToList();
    }

    public async Task<AdminDoctorVerificationResponse?> ApproveAsync(Guid requestId, Guid reviewerUserId, string? comment, CancellationToken cancellationToken = default)
        => await ReviewAsync(requestId, reviewerUserId, DoctorVerificationStatus.Approved, comment, cancellationToken);

    public async Task<AdminDoctorVerificationResponse?> RejectAsync(Guid requestId, Guid reviewerUserId, string comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length < 10)
            throw new ArgumentException("Для отклонения укажите понятную причину не короче 10 символов.");
        return await ReviewAsync(requestId, reviewerUserId, DoctorVerificationStatus.Rejected, comment, cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> OpenDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.DoctorVerificationDocuments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
            return null;
        var path = _uploadPaths.GetDoctorVerificationFilePath(document.StoredFileName);
        if (!File.Exists(path))
            return null;
        return (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan), document.ContentType, document.OriginalFileName);
    }

    private async Task<AdminDoctorVerificationResponse?> ReviewAsync(Guid requestId, Guid reviewerUserId, DoctorVerificationStatus status, string? comment, CancellationToken cancellationToken)
    {
        var request = await Query().FirstOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
        if (request is null)
            return null;
        if (request.Status != DoctorVerificationStatus.Submitted)
            throw new InvalidOperationException("Заявка уже обработана.");

        request.Status = status;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewComment = Normalize(comment);
        if (status == DoctorVerificationStatus.Approved)
            request.User.Role = UserRole.Doctor;
        else
        {
            request.User.IsActive = false;
            request.User.DeactivatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync($"doctor_verification.{status.ToString().ToLowerInvariant()}", "DoctorVerificationRequest", request.RequestId.ToString("D"), $"user={request.UserId}", cancellationToken);
        await SendDecisionEmailAsync(request, cancellationToken);
        return MapAdmin(request);
    }

    private IQueryable<DoctorVerificationRequest> Query() => _db.DoctorVerificationRequests
        .Include(item => item.User)
        .Include(item => item.Documents);

    private async Task NotifyAdministratorsAsync(User user, DoctorVerificationRequest request, CancellationToken cancellationToken)
    {
        var administrators = await _db.Users
            .Where(item => item.IsActive && item.Role == UserRole.Admin)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        foreach (var administratorId in administrators)
        {
            _db.UserNotifications.Add(new UserNotification
            {
                RecipientUserId = administratorId,
                Type = "doctor_verification",
                Title = "Новая заявка врача",
                Description = $"{user.EmailForLogin}: {request.Specialty}",
                SourceType = "doctor_verification_request",
                SourceId = request.RequestId,
                CreatedAt = request.SubmittedAt ?? DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendDecisionEmailAsync(DoctorVerificationRequest request, CancellationToken cancellationToken)
    {
        var email = request.User.EmailForLogin;
        if (string.IsNullOrWhiteSpace(email)) return;
        var approved = request.Status == DoctorVerificationStatus.Approved;
        var subject = approved ? "SugarGuard: профиль врача подтверждён" : "SugarGuard: заявка врача требует доработки";
        var message = approved
            ? "Ваши документы проверены. Доступ к кабинету врача открыт."
            : $"Заявка на регистрацию врача отклонена. Причина: {request.ReviewComment}";
        try
        {
            await _email.SendAsync(email, subject, $"<p>{WebUtility.HtmlEncode(message)}</p>", message, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось отправить результат проверки врачу. RequestId={RequestId}", request.RequestId);
        }
    }

    private async Task<PreparedFile> ValidateAndReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxDocumentBytes || !AllowedTypes.TryGetValue(file.ContentType, out var declaredExtension))
            throw new ArgumentException("Допустимы PDF, JPEG и PNG размером до 10 МБ.");
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var actualExtension = DetectExtension(bytes);
        if (actualExtension is null || !string.Equals(actualExtension, declaredExtension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Содержимое файла не соответствует заявленному формату.");
        return new PreparedFile(Path.GetFileName(file.FileName), file.ContentType, actualExtension, bytes);
    }

    private static string? DetectExtension(byte[] bytes) => bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ? ".pdf"
        : bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF ? ".jpg"
        : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ? ".png"
        : null;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void DeleteStoredFile(string storedFileName)
    {
        var path = _uploadPaths.GetDoctorVerificationFilePath(storedFileName);
        if (File.Exists(path)) File.Delete(path);
    }

    private DoctorVerificationResponse Map(DoctorVerificationRequest request) => new()
    {
        RequestId = request.RequestId, UserId = request.UserId, Status = request.Status.ToString(), Specialty = request.Specialty,
        LicenseNumber = Decrypt(request.EncryptedLicenseNumber), OrganizationName = request.OrganizationName, Comment = request.Comment,
        SubmittedAt = request.SubmittedAt, ReviewedAt = request.ReviewedAt, ReviewComment = request.ReviewComment,
        Documents = request.Documents.OrderBy(item => item.UploadedAt).Select(MapDocument).ToList()
    };

    private AdminDoctorVerificationResponse MapAdmin(DoctorVerificationRequest request) => new()
    {
        RequestId = request.RequestId, UserId = request.UserId, Status = request.Status.ToString(), Specialty = request.Specialty,
        LicenseNumber = Decrypt(request.EncryptedLicenseNumber), OrganizationName = request.OrganizationName, Comment = request.Comment,
        SubmittedAt = request.SubmittedAt, ReviewedAt = request.ReviewedAt, ReviewComment = request.ReviewComment,
        Documents = request.Documents.OrderBy(item => item.UploadedAt).Select(MapDocument).ToList(),
        Email = request.User.EmailForLogin ?? string.Empty
    };

    private static DoctorVerificationDocumentResponse MapDocument(DoctorVerificationDocument document) => new() { DocumentId = document.DocumentId, FileName = document.OriginalFileName, ContentType = document.ContentType, SizeBytes = document.SizeBytes, UploadedAt = document.UploadedAt };
    private string Decrypt(string value) { try { return _crypto.Decrypt(value); } catch { return string.Empty; } }
    private sealed record PreparedFile(string OriginalFileName, string ContentType, string Extension, byte[] Content);
}
