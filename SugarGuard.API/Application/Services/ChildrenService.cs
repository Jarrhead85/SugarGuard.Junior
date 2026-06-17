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
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
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

        if (role is UserRole.Admin or UserRole.SupportAdmin or UserRole.ServiceAccount)
        {
            // без фильтра — админ видит всех
        }
        else if (role == UserRole.Doctor)
        {
            query = query.Where(c => db.DoctorChildLinks
                .Any(l => l.DoctorUserId == userId && l.ChildId == c.ChildId && l.IsActive));
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
                DiagnosisDate = c.DiagnosisDate
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
            DiagnosisDate = request.DiagnosisDate,
            InsulinScheme = request.InsulinScheme?.Trim(),
            CurrentInsulins = "[]",
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                ? "UTC"
                : request.TimeZoneId.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl)
                ? null
                : request.PhotoUrl.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Children.Add(child);

        db.DiabetesSettings.Add(new DiabetesSettings
        {
            ChildId = child.ChildId,
            UpdatedAt = now
        });

        if (role == UserRole.Parent)
        {
            parentLinkId = Guid.NewGuid();
            db.ParentChildLinks.Add(new ParentChildLink
            {
                LinkId = parentLinkId.Value,
                ParentUserId = userId,
                ChildId = child.ChildId,
                CreatedAt = now
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
        child.PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl)
            ? child.PhotoUrl
            : request.PhotoUrl.Trim();
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
                "Недопустимый формат файла. Разрешены: jpg, jpeg, png, webp, gif.");

        if (!string.IsNullOrEmpty(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException(
                $"Недопустимый Content-Type: {file.ContentType}.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);

        if (child is null)
            return null;

        // Генерируем уникальный относительный путь: uploads/children/{childId}/{guid}.{ext}
        var uniqueFileName = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
        var childDir = Path.Combine(uploadRoot, "uploads", "children", childId.ToString());
        var absolutePath = Path.Combine(childDir, uniqueFileName);
        var relativeUrl = $"/uploads/children/{childId}/{uniqueFileName}";

        Directory.CreateDirectory(childDir);

        // Удаляем старый файл, если он указывает на локальный путь
        TryDeleteLocalFile(child.PhotoUrl, uploadRoot);

        // Сохраняем новый файл атомарно: пишем во временный .tmp, затем File.Move.
        var tempPath = absolutePath + ".tmp";
        await using (var stream = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        File.Move(tempPath, absolutePath, overwrite: true);

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

        TryDeleteLocalFile(child.PhotoUrl, uploadRoot);

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
    private static void TryDeleteLocalFile(string? photoUrl, string uploadRoot)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            return;

        // Только локальные относительные пути
        if (photoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            photoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return;

        var trimmed = photoUrl.TrimStart('/');
        var absolute = Path.Combine(uploadRoot, trimmed);

        // Защита от path traversal: итоговый путь должен оставаться внутри uploadRoot.
        var fullRoot = Path.GetFullPath(uploadRoot);
        var fullFile = Path.GetFullPath(absolute);
        if (!fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (File.Exists(fullFile))
                File.Delete(fullFile);
        }
        catch
        {

        }
    }

    private static ChildResponse MapToResponse(Child child) => new()
    {
        ChildId = child.ChildId,
        FirstName = child.FirstName,
        LastName = child.LastName,
        DateOfBirth = child.DateOfBirth,
        Weight = child.Weight,
        Height = child.Height,
        DiabetesType = child.DiabetesType,
        DiagnosisDate = child.DiagnosisDate,
        InsulinScheme = child.InsulinScheme,
        CurrentInsulins = child.CurrentInsulins,
        TimeZoneId = child.TimeZoneId,
        CreatedAt = child.CreatedAt,
        UpdatedAt = child.UpdatedAt,
        PhotoUrl = child.PhotoUrl
    };
}
