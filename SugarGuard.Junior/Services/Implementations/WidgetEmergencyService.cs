using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Sends a parent alert only after current coordinates have been acquired. The
/// widget never displays glucose or location, so no PHI is exposed on the lock screen.
/// </summary>
public sealed class WidgetEmergencyService : IWidgetEmergencyService
{
    private readonly IStorageService _storage;
    private readonly ILocationService _location;
    private readonly IApiClient _api;
    private readonly ILogger<WidgetEmergencyService> _logger;

    public WidgetEmergencyService(IStorageService storage, ILocationService location, IApiClient api, ILogger<WidgetEmergencyService> logger)
    {
        _storage = storage;
        _location = location;
        _api = api;
        _logger = logger;
    }

    public async Task<bool> SendSosAsync(CancellationToken cancellationToken = default)
    {
        var childId = await _storage.GetAsync(Constants.StorageKeyCurrentChildId);
        if (string.IsNullOrWhiteSpace(childId)) return false;

        // A background widget cannot display the Android permission prompt. The
        // child grants location access in the app once; otherwise we fail closed.
        if (!await _location.IsLocationPermissionGrantedAsync()) return false;
        var position = await _location.GetCurrentLocationAsync(TimeSpan.FromSeconds(12));
        if (position is null) return false;

        var rawGlucose = await _storage.GetAsync(Constants.StorageKeyLastGlucoseValue);
        var glucose = !string.IsNullOrWhiteSpace(rawGlucose)
                      && DoubleParser.TryParseDecrypted(rawGlucose, out var parsedGlucose)
            ? parsedGlucose
            : 0d;

        var sent = await _api.SendCriticalAlertAsync(new CriticalAlertRequest
        {
            ChildId = childId,
            GlucoseValue = glucose,
            MeasurementTime = DateTime.UtcNow,
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Address = position.Address,
            IsEmergencyHelp = true
        });

        if (!sent) _logger.LogWarning("SOS widget alert was not delivered for child {ChildId}.", childId);
        return sent;
    }
}
