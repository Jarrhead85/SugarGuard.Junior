using SugarGuard.API.DTOs;

namespace SugarGuard.API.Services;

public interface IUserNotificationService
{
    Task<IReadOnlyList<UserNotificationDto>> GetForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    Task PersistCriticalLocationAsync(
        CriticalAlertRequest request,
        CancellationToken cancellationToken = default);

    Task PersistMeasurementAsync(
        Guid childId,
        Guid measurementId,
        decimal glucoseValue,
        string status,
        DateTime measuredAt,
        bool isCritical,
        CancellationToken cancellationToken = default);

    Task PersistSnackConsumedAsync(
        Guid childId,
        Guid backpackItemId,
        string snackName,
        decimal breadUnits,
        double currentGlucose,
        DateTime consumedAt,
        CancellationToken cancellationToken = default);
}
