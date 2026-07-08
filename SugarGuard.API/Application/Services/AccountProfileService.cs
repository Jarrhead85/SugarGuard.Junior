using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Security;
using SugarGuard.Application.Audit;

namespace SugarGuard.API.Application.Services;

public sealed class AccountProfileService : IAccountProfileService
{
    private const long MaxPhotoBytes = 5L * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedPhotoTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    private readonly AppDbContext _db;
    private readonly ICryptoService _crypto;
    private readonly IUploadPathProvider _uploadPaths;
    private readonly IAuditService _audit;

    public AccountProfileService(
        AppDbContext db,
        ICryptoService crypto,
        IUploadPathProvider uploadPaths,
        IAuditService audit)
    {
        _db = db;
        _crypto = crypto;
        _uploadPaths = uploadPaths;
        _audit = audit;
    }

    public async Task<AccountProfileResponse?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return user is null ? null : Map(user);
    }

    public async Task<AccountProfileResponse?> UpdateAsync(
        Guid userId,
        UpdateAccountProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.EncryptedFirstName = _crypto.Encrypt(request.FirstName.Trim());
        user.EncryptedLastName = string.IsNullOrWhiteSpace(request.LastName)
            ? null
            : _crypto.Encrypt(request.LastName.Trim());
        user.DoctorSpecialty = string.IsNullOrWhiteSpace(request.Specialty)
            ? null
            : request.Specialty.Trim();
        user.EncryptedDoctorLicense = string.IsNullOrWhiteSpace(request.LicenseNumber)
            ? null
            : _crypto.Encrypt(request.LicenseNumber.Trim());

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("account.profile.updated", "User", userId.ToString("D"), cancellationToken: cancellationToken);
        return Map(user);
    }

    public async Task<string> UploadPhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > MaxPhotoBytes)
        {
            throw new ArgumentException("Размер фотографии должен быть от 1 байта до 5 МБ.");
        }

        if (!AllowedPhotoTypes.TryGetValue(file.ContentType, out var extension))
        {
            throw new ArgumentException("Допустимы фотографии JPEG, PNG или WebP.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Пользователь не найден.");
        var uploadDirectory = _uploadPaths.ProfilesDirectory;
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{userId:N}-{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(uploadDirectory, fileName);
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        DeleteLocalPhoto(user.ProfilePhotoUrl, uploadDirectory);
        user.ProfilePhotoUrl = $"/uploads/profiles/{fileName}";
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("account.photo.updated", "User", userId.ToString("D"), cancellationToken: cancellationToken);
        return user.ProfilePhotoUrl;
    }

    public async Task<bool> DeletePhotoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.ProfilePhotoUrl))
        {
            return false;
        }

        DeleteLocalPhoto(user.ProfilePhotoUrl, _uploadPaths.ProfilesDirectory);
        user.ProfilePhotoUrl = null;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private AccountProfileResponse Map(SugarGuard.Domain.Entities.User user)
    {
        var firstName = Decrypt(user.EncryptedFirstName);
        var lastName = Decrypt(user.EncryptedLastName);
        var displayName = string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.EmailForLogin ?? "Пользователь SugarGuard";
        }

        return new AccountProfileResponse
        {
            UserId = user.UserId,
            Email = user.EmailForLogin ?? string.Empty,
            Role = user.Role.ToString(),
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            PhotoUrl = GetAvailablePhotoUrl(user.ProfilePhotoUrl),
            Specialty = user.DoctorSpecialty,
            LicenseNumber = Decrypt(user.EncryptedDoctorLicense)
        };
    }

    private string? GetAvailablePhotoUrl(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        if (!photoUrl.StartsWith("/uploads/profiles/", StringComparison.Ordinal))
        {
            return photoUrl;
        }

        return File.Exists(_uploadPaths.GetProfileFilePath(Path.GetFileName(photoUrl)))
            ? photoUrl
            : null;
    }

    private string Decrypt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            return _crypto.Decrypt(value);
        }
        catch (Exception exception) when (exception is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return value;
        }
    }

    private static void DeleteLocalPhoto(string? photoUrl, string uploadDirectory)
    {
        if (string.IsNullOrWhiteSpace(photoUrl) || !photoUrl.StartsWith("/uploads/profiles/", StringComparison.Ordinal))
        {
            return;
        }

        var fileName = Path.GetFileName(photoUrl);
        var path = Path.GetFullPath(Path.Combine(uploadDirectory, fileName));
        var root = Path.GetFullPath(uploadDirectory) + Path.DirectorySeparatorChar;
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
