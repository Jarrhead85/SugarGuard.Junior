using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.Services.Implementations;

public class CurrentUserService : ICurrentUserService
{
    private readonly ISecureStorageService _secureStorage;
    private readonly IStorageService _storageService;

    public CurrentUserService(
        ISecureStorageService secureStorage,
        IStorageService storageService)
    {
        _secureStorage = secureStorage;
        _storageService = storageService;
    }

    public async Task<string?> GetCurrentUserIdAsync()
    {
        var userId = await _secureStorage.GetAsync(AppConstants.StorageKeyCurrentUserId);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        // AuthenticationService historically wrote this key through
        // IStorageService (without SecureStorageService's key_ prefix). Keep a
        // local fallback here as well because BackpackPage may request the id
        // before an older installation has completed the auth migration.
        userId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentUserId);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await _secureStorage.SaveAsync(AppConstants.StorageKeyCurrentUserId, userId);
        }

        return userId;
    }
}
