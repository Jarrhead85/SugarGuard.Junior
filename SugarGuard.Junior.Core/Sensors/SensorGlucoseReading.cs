namespace SugarGuard.Junior.Core.Sensors;

/// <summary>
/// Describes whether the application can authenticate the origin of a sensor reading.
/// Android implicit broadcasts cannot prove which application originally sent them.
/// </summary>
public enum SensorReadingTrust
{
    UntrustedExternalBroadcast = 0,
    ConfirmedLocallyByUser = 1,
    AuthenticatedProvider = 2
}

/// <summary>
/// Унифицированное показание, полученное от внешнего источника глюкозы.
/// Время всегда хранится в UTC, чтобы офлайн-синхронизация не зависела от часового пояса телефона.
/// </summary>
public sealed record SensorGlucoseReading(
    double GlucoseMmolPerLiter,
    DateTime MeasurementTimeUtc,
    DateTime ReceivedAtUtc,
    string Source,
    string? SensorSerialNumber,
    double? RateMmolPerLiterPerMinute,
    SensorReadingTrust Trust = SensorReadingTrust.UntrustedExternalBroadcast);

/// <summary>
/// Central trust boundary for readings received from external applications.
/// Unauthenticated broadcasts may be displayed as pending data, but must not
/// drive advice, SOS data or critical notifications until confirmed locally.
/// </summary>
public static class SensorGlucoseTrustPolicy
{
    public static readonly TimeSpan MaximumConfirmationAge = TimeSpan.FromMinutes(15);

    public static bool IsTrustedForMedicalUse(SensorGlucoseReading reading) =>
        reading.Trust is SensorReadingTrust.ConfirmedLocallyByUser or SensorReadingTrust.AuthenticatedProvider;

    public static bool TryConfirmLocally(
        SensorGlucoseReading reading,
        DateTime utcNow,
        out SensorGlucoseReading? confirmedReading,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(reading);

        confirmedReading = null;
        error = null;

        var normalizedNow = EnsureUtc(utcNow);
        var normalizedReceivedAt = EnsureUtc(reading.ReceivedAtUtc);

        if (reading.Trust != SensorReadingTrust.UntrustedExternalBroadcast)
        {
            error = "Показание уже имеет доверенный источник.";
            return false;
        }

        if (normalizedReceivedAt > normalizedNow.AddMinutes(1) ||
            normalizedReceivedAt < normalizedNow.Subtract(MaximumConfirmationAge))
        {
            error = "Показание устарело. Дождитесь нового значения датчика.";
            return false;
        }

        confirmedReading = reading with
        {
            ReceivedAtUtc = normalizedReceivedAt,
            MeasurementTimeUtc = EnsureUtc(reading.MeasurementTimeUtc),
            Trust = SensorReadingTrust.ConfirmedLocallyByUser
        };
        return true;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
