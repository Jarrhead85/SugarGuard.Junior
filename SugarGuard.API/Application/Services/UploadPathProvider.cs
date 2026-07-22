using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

public sealed class UploadPathProvider : IUploadPathProvider
{
    public UploadPathProvider(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["Storage:UploadsRoot"];
        var uploadsRoot = !string.IsNullOrWhiteSpace(configuredRoot)
            ? configuredRoot
            : OperatingSystem.IsLinux()
                ? "/var/lib/sugarguard/uploads"
                : Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");

        ProfilesDirectory = Path.GetFullPath(Path.Combine(uploadsRoot, "profiles"));
        DoctorVerificationDirectory = Path.GetFullPath(Path.Combine(uploadsRoot, "doctor-verification"));
        ArticleImagesDirectory = Path.GetFullPath(Path.Combine(uploadsRoot, "article-images"));
    }

    public string ProfilesDirectory { get; }

    public string DoctorVerificationDirectory { get; }

    public string ArticleImagesDirectory { get; }

    public string GetProfileFilePath(string fileName)
        => GetSafePath(ProfilesDirectory, fileName);

    public string GetDoctorVerificationFilePath(string fileName)
        => GetSafePath(DoctorVerificationDirectory, fileName);

    public string GetArticleImageFilePath(string fileName)
        => GetSafePath(ArticleImagesDirectory, fileName);

    private static string GetSafePath(string directory, string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Некорректное имя файла.", nameof(fileName));
        }

        return Path.Combine(directory, safeName);
    }
}
