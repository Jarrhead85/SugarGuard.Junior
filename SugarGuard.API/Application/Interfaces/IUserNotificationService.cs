using SugarGuard.API.DTOs;

namespace SugarGuard.API.Services;

public interface IUserNotificationService
{
    Task<IReadOnlyList<UserNotificationDto>> GetForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}
