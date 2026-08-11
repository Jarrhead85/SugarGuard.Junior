using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация CRUD для таблицы детей
/// </summary>
public sealed class ChildrenService : IChildrenService
{
    private const decimal DefaultWeightKg = 30m;
    private const decimal DefaultHeightCm = 130m;

    /// <summary>
    /// Максимальный размер фото 5 МБ
    /// </summary>
    private const long MaxPhotoBytes = 5L * 1024 * 1024;

    /// <summary>
    /// Разрешённые расширения фото
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;

    public ChildrenService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit)
    {
        _dbFactory = dbFactory;
        _audit = audit;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ChildSummaryResponse>> GetAccessibleAsync(
        Guid userId,
        UserRole role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Child> query = db.Children.AsNoTracking();

        if (role is UserRole.Admin or UserRole.SupportAdmin)
        {
            // без фильтра — админ видит всех
        }
        else if (role == UserRole.Doctor)
        {
            query = query.Where(c => db.DoctorChildLinks
                .Any(l => l.DoctorUserId == userId && l.ChildId == c.ChildId && l.IsActive));
        }
        else if (role == UserRole.Patient)
        {
            query = query.Where(c => db.ParentChildLinks
                .Any(l => l.ParentUserId == userId
                          && l.ChildId == c.ChildId
                          && l.LinkType == ParentChildLinkType.SelfLinkPatient));
        }
        else
        {
            query = query.Where(c => db.ParentChildLinks
                .Any(l => l.ParentUserId == userId && l.ChildId == c.ChildId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(c => new ChildSummaryResponse
            {
                ChildId = c.ChildId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                DateOfBirth = c.DateOfBirth,
                DiabetesType = c.DiabetesType,
                DiagnosisDate = c.DiagnosisDate,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ChildSummaryResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    /// <inheritdoc/>
    public async Task<ChildResponse?> GetByIdAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);

        return child is null ? null : MapToResponse(child);
    }

    /// <inheritdoc/>
    public async Task<CreateChildResult> CreateAsync(
        Guid userId,
        UserRole role,
        CreateChildRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (role is UserRole.ChildDevice or UserRole.Patient)
        {
            var existingSelfLink = await db.ParentChildLinks
                .Where(l => l.ParentUserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingSelfLink is not null)
            {
                var existingChild = await db.Children
                    .FirstOrDefaultAsync(
                        c => c.ChildId == existingSelfLink.ChildId,
                        cancellationToken);

                if (existingChild is not null)
                {
                    ApplyChildRequest(existingChild, request, now);
                    existingChild.SetupCompleted = true;
                    existingChild.SetupCompletedAt ??= now;

                    var hasSettings = await db.DiabetesSettings
                        .AnyAsync(d => d.ChildId == existingChild.ChildId, cancellationToken);

                    if (!hasSettings)
                    {
                        db.DiabetesSettings.Add(new DiabetesSettings
                        {
                            ChildId = existingChild.ChildId,
                            UpdatedAt = now
                        });
                    }

                    await db.SaveChangesAsync(cancellationToken);

                    await _audit.WriteAsync(
                        action: "child.updated",
                        targetType: "Child",
                        targetId: existingChild.ChildId.ToString(),
                        details: $"SelfManagedUser={userId};Role={role};Source=CreateAsyncExistingSelfLink",
                        cancellationToken: cancellationToken);

                    return new CreateChildResult
                    {
                        Child = MapToResponse(existingChild),
                        ParentLinkId = existingSelfLink.LinkId
                    };
                }
            }
        }

        Guid? parentLinkId = null;
        var child = new Child
        {
            ChildId = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Weight = request.Weight ?? DefaultWeightKg,
            Height = request.Height ?? DefaultHeightCm,
            DiabetesType = request.DiabetesType.Trim(),
            CareMode = role == UserRole.Patient
                ? PatientCareMode.SelfManaged
                : PatientCareMode.ChildWithGuardian,
            DiagnosisDate = request.DiagnosisDate,
            InsulinScheme = request.InsulinScheme?.Trim(),
            CurrentInsulins = "[]",
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                ? "UTC"
                : request.TimeZoneId.Trim(),
            // PhotoUrl is server-owned. Clients must use the dedicated upload endpoint.
            PhotoUrl = null,
            CreatedAt = now,
            UpdatedAt = now,
            SetupCompleted = role is UserRole.ChildDevice or UserRole.Patient,
            SetupCompletedAt = role is UserRole.ChildDevice or UserRole.Patient ? now : null
        };

        db.Children.Add(child);

        db.DiabetesSettings.Add(new DiabetesSettings
        {
            ChildId = child.ChildId,
            UpdatedAt = now
        });

        if (role is UserRole.Parent or UserRole.ChildDevice or UserRole.Patient)
        {
            parentLinkId = Guid.NewGuid();
            db.ParentChildLinks.Add(new ParentChildLink
            {
                LinkId = parentLinkId.Value,
                ParentUserId = userId,
                ChildId = child.ChildId,
                CreatedAt = now,
                LinkedByUserId = userId,
                LinkType = role switch
                {
                    UserRole.ChildDevice => ParentChildLinkType.SelfLinkChildDevice,
                    UserRole.Patient => ParentChildLinkType.SelfLinkPatient,
                    _ => ParentChildLinkType.Regular
                }
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "child.created",
            targetType: "Child",
            targetId: child.ChildId.ToString(),
            details: $"Parent={userId};Role={role}",
            cancellationToken: cancellationToken);

        return new CreateChildResult
        {
            Child = MapToResponse(child),
            ParentLinkId = parentLinkId
        };
    }

    private static void ApplyChildRequest(Child child, CreateChildRequest request, DateTime updatedAt)
    {
        child.FirstName = request.FirstName.Trim();
        child.LastName = request.LastName.Trim();
        child.DateOfBirth = request.DateOfBirth;
        child.Weight = request.Weight ?? child.Weight;
        child.Height = request.Height ?? child.Height;
        child.DiabetesType = request.DiabetesType.Trim();
        child.DiagnosisDate = request.DiagnosisDate;
        child.InsulinScheme = request.InsulinScheme?.Trim();
        child.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? child.TimeZoneId
            : request.TimeZoneId.Trim();
        child.UpdatedAt = updatedAt;
    }

    /// <inheritdoc/>
    public async Task<ChildResponse?> UpdateAsync(
        Guid childId,
        UpdateChildRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);

        if (child is null)
            return null;

        child.FirstName = request.FirstName.Trim();
        child.LastName = request.LastName.Trim();
        child.DateOfBirth = request.DateOfBirth;
        child.Weight = request.Weight;
        child.Height = request.Height;
        child.DiabetesType = request.DiabetesType.Trim();
        child.DiagnosisDate = request.DiagnosisDate;
        child.InsulinScheme = request.InsulinScheme?.Trim();
        child.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? child.TimeZoneId
            : request.TimeZoneId.Trim();
        child.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "child.updated",
            targetType: "Child",
            targetId: child.ChildId.ToString(),
            cancellationToken: cancellationToken);

        return MapToResponse(child);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteChildAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);

        if (child is null)
            return false;

        db.Children.Remove(child);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "child.deleted",
            targetType: "Child",
            targetId: childId.ToString(),
            cancellationToken: cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<string?> UploadPhotoAsync(
        Guid childId,
        IFormFile file,
        string uploadRoot,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return null;

        if (file.Length > MaxPhotoBytes)
            throw new InvalidOperationException(
                $"Размер файла превышает {MaxPhotoBytes / 1024 / 1024} МБ.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException(
                "Недопустимый формат файла. Разрешены: jpg, jpeg, png, webp.");

        if (!string.IsNullOrEmpty(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException(
                $"Недопустимый Content-Type: {file.ContentType}.");

        await using var source = file.OpenReadStream();
        await using var bufferedImage = new MemoryStream((int)file.Length);
        await source.CopyToAsync(bufferedImage, cancellationToken);

        var detected = DetectImageFormat(bufferedImage.GetBuffer().AsSpan(0, (int)bufferedImage.Length));
        if (detected is null
            || !IsExtensionCompatible(extension, detected.Value.Extension)
            || (!string.IsNullOrEmpty(file.ContentType)
                && !string.Equals(file.ContentType, detected.Value.ContentType, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Содержимое файла не соответствует поддерживаемому формату изображения.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);

        if (child is null)
            return null;

        // Генерируем уникальный относительный путь: uploads/children/{childId}/{guid}.{ext}
        var uniqueFileName = $"{Guid.NewGuid()}{detected.Value.Extension}";
        var childDir = Path.Combine(uploadRoot, "uploads", "children", childId.ToString());
        var absolutePath = Path.Combine(childDir, uniqueFileName);
        var relativeUrl = $"/uploads/children/{childId}/{uniqueFileName}";

        Directory.CreateDirectory(childDir);

        // Удаляем старый файл, если он указывает на локальный путь
        TryDeleteLocalFile(child.PhotoUrl, uploadRoot, childId);

        // Сохраняем новый файл атомарно: пишем во временный .tmp, затем File.Move.
        var tempPath = absolutePath + ".tmp";
        try
        {
            bufferedImage.Position = 0;
            await using (var stream = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                await bufferedImage.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, absolutePath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        child.PhotoUrl = relativeUrl;
        child.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "child.photo.uploaded",
            targetType: "Child",
            targetId: child.ChildId.ToString(),
            details: $"PhotoUrl={relativeUrl};Size={file.Length}",
            cancellationToken: CancellationToken.None);

        return relativeUrl;
    }

    /// <inheritdoc/>
    public async Task<bool> DeletePhotoAsync(
        Guid childId,
        string uploadRoot,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);

        if (child is null || string.IsNullOrEmpty(child.PhotoUrl))
            return false;

        TryDeleteLocalFile(child.PhotoUrl, uploadRoot, childId);

        child.PhotoUrl = null;
        child.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "child.photo.deleted",
            targetType: "Child",
            targetId: child.ChildId.ToString(),
            cancellationToken: CancellationToken.None);

        return true;
    }

    /// <summary>
    /// Удаляет локальный файл
    /// </summary>
    private static void TryDeleteLocalFile(string? photoUrl, string uploadRoot, Guid childId)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            return;

        var expectedPrefix = $"/uploads/children/{childId:D}/";
        if (!photoUrl.StartsWith(expectedPrefix, StringComparison.Ordinal)
            || photoUrl.Length <= expectedPrefix.Length)
        {
            return;
        }

        var fileName = photoUrl[expectedPrefix.Length..];
        if (fileName.Contains('/') || fileName.Contains('\\'))
            return;

        var extension = Path.GetExtension(fileName);
        if (!Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _)
            || !AllowedExtensions.Contains(extension))
        {
            return;
        }

        var childDirectory = Path.GetFullPath(
            Path.Combine(uploadRoot, "uploads", "children", childId.ToString("D")));
        var fullFile = Path.GetFullPath(Path.Combine(childDirectory, fileName));
        var childDirectoryPrefix = childDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullFile.StartsWith(childDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (File.Exists(fullFile))
                File.Delete(fullFile);
        }
        catch (IOException)
        {
            // A failed cleanup must not make the profile operation unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Permissions are reported by infrastructure monitoring; do not leak paths.
        }
    }

    private static (string Extension, string ContentType)? DetectImageFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8
            && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return (".png", "image/png");
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return (".webp", "image/webp");
        }

        return null;
    }

    private static bool IsExtensionCompatible(string suppliedExtension, string detectedExtension) =>
        string.Equals(suppliedExtension, detectedExtension, StringComparison.OrdinalIgnoreCase)
        || (string.Equals(detectedExtension, ".jpg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(suppliedExtension, ".jpeg", StringComparison.OrdinalIgnoreCase));

    private static ChildResponse MapToResponse(Child child) => new()
    {
        ChildId = child.ChildId,
        FirstName = child.FirstName,
        LastName = child.LastName,
        DateOfBirth = child.DateOfBirth,
        Weight = child.Weight,
        Height = child.Height,
        DiabetesType = child.DiabetesType,
        CareMode = child.CareMode,
        DiagnosisDate = child.DiagnosisDate,
        InsulinScheme = child.InsulinScheme,
        CurrentInsulins = child.CurrentInsulins,
        TimeZoneId = child.TimeZoneId,
        CreatedAt = child.CreatedAt,
        UpdatedAt = child.UpdatedAt,
        PhotoUrl = child.PhotoUrl
    };
}
