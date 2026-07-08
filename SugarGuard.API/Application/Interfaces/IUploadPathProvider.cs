namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Resolves persistent storage paths for user-uploaded files.
/// </summary>
public interface IUploadPathProvider
{
    string ProfilesDirectory { get; }

    string GetProfileFilePath(string fileName);
}
