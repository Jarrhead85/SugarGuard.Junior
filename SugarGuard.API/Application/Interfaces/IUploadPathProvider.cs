namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Resolves persistent storage paths for user-uploaded files.
/// </summary>
public interface IUploadPathProvider
{
    string ProfilesDirectory { get; }

    string DoctorVerificationDirectory { get; }

    string ArticleImagesDirectory { get; }

    string GetProfileFilePath(string fileName);

    string GetDoctorVerificationFilePath(string fileName);

    string GetArticleImageFilePath(string fileName);
}
