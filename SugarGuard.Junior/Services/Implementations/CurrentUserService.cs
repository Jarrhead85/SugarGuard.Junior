using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.Services.Implementations;

public class CurrentUserService : ICurrentUserService
{
    private readonly ISecureStorageService _secureStorage;

    public CurrentUserService(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public Task<string?> GetCurrentUserIdAsync() =>
        _secureStorage.GetAsync(AppConstants.StorageKeyCurrentUserId);
}
