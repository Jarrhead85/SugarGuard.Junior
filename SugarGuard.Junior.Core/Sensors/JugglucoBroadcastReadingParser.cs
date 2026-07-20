namespace SugarGuard.Junior.Core.Sensors;

/// <summary>
/// Контракт локального broadcast, который публикует Juggluco.
/// Класс не содержит кода доступа к Libre: он разбирает только документированный обмен данными Android-приложений.
/// </summary>
public static class JugglucoBroadcastContract
{
    public const string GlucodataMinuteAction = "glucodata.Minute";
    public const string XdripBgEstimateAction = "com.eveningoutpost.dexdrip.BgEstimate";

    public const string GlucoseExtra = "glucodata.Minute.glucose";
    public const string MgDlExtra = "glucodata.Minute.mgdl";
    public const string RateExtra = "glucodata.Minute.Rate";
    public const string SerialNumberExtra = "glucodata.Minute.SerialNumber";
    public const string TimeExtra = "glucodata.Minute.Time";

    public const string XdripGlucoseExtra = "bg";
    public const string XdripTimestampExtra = "timestamp";
    public const string XdripDeltaExtra = "delta";
}

/// <summary>
/// Значения extras из Android broadcast, приведённые к нейтральным типам до разбора.
/// </summary>
public sealed record JugglucoBroadcastPayload(
    string Action,
    double? Glucose,
    double? MgDl,
    long? TimestampUnixMilliseconds,
    double? Rate,
    string? SensorSerialNumber);

/// <summary>
/// Преобразует broadcast Juggluco/xDrip в единый формат SugarGuard.
/// </summary>
public static class JugglucoBroadcastReadingParser
{
    private const double MgDlPerMmol = 18.0182;
    private const double MinimumMmolPerLiter = 1.0;
    private const double MaximumMmolPerLiter = 30.0;
    private const double MaximumMgDl = 600.0;

    /// <summary>
    /// Разбирает одно входящее сообщение и проверяет границы, не выполняя медицинских рекомендаций.
    /// </summary>
    public static bool TryParse(
        JugglucoBroadcastPayload payload,
        DateTime receivedAtUtc,
        out SensorGlucoseReading? reading,
        out string? error)
    {
        reading = null;
        error = null;

        if (payload is null)
        {
            error = "Получено пустое сообщение датчика.";
            return false;
        }

        if (payload.Action is not JugglucoBroadcastContract.GlucodataMinuteAction and not JugglucoBroadcastContract.XdripBgEstimateAction)
        {
            error = "Неизвестный тип сообщения источника глюкозы.";
            return false;
        }

        if (!TryGetGlucoseInMmol(payload, out var glucoseMmol))
        {
            error = "Сообщение датчика не содержит корректного значения глюкозы.";
            return false;
        }

        var normalizedReceivedAt = EnsureUtc(receivedAtUtc);
        var measurementTime = TryGetMeasurementTime(payload.TimestampUnixMilliseconds, normalizedReceivedAt, out var parsedTime)
            ? parsedTime
            : normalizedReceivedAt;

        if (measurementTime > normalizedReceivedAt.AddMinutes(15))
        {
            error = "Время измерения датчика находится слишком далеко в будущем.";
            return false;
        }

        reading = new SensorGlucoseReading(
            glucoseMmol,
            measurementTime,
            normalizedReceivedAt,
            "Juggluco",
            string.IsNullOrWhiteSpace(payload.SensorSerialNumber) ? null : payload.SensorSerialNumber.Trim(),
            NormalizeRate(payload.Rate));

        return true;
    }

    private static bool TryGetGlucoseInMmol(JugglucoBroadcastPayload payload, out double glucoseMmol)
    {
        glucoseMmol = 0;

        if (payload.MgDl is > 0 and <= MaximumMgDl)
        {
            glucoseMmol = Math.Round(payload.MgDl.Value / MgDlPerMmol, 1, MidpointRounding.AwayFromZero);
            return glucoseMmol is >= MinimumMmolPerLiter and <= MaximumMmolPerLiter;
        }

        if (payload.Glucose is not > 0)
        {
            return false;
        }

        var rawGlucose = payload.Glucose.Value;
        glucoseMmol = rawGlucose > MaximumMmolPerLiter
            ? Math.Round(rawGlucose / MgDlPerMmol, 1, MidpointRounding.AwayFromZero)
            : Math.Round(rawGlucose, 1, MidpointRounding.AwayFromZero);

        return glucoseMmol is >= MinimumMmolPerLiter and <= MaximumMmolPerLiter;
    }

    private static bool TryGetMeasurementTime(long? timestampUnixMilliseconds, DateTime receivedAtUtc, out DateTime measurementTimeUtc)
    {
        measurementTimeUtc = receivedAtUtc;
        if (!timestampUnixMilliseconds.HasValue || timestampUnixMilliseconds.Value <= 0)
        {
            return false;
        }

        try
        {
            measurementTimeUtc = DateTimeOffset
                .FromUnixTimeMilliseconds(timestampUnixMilliseconds.Value)
                .UtcDateTime;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static double? NormalizeRate(double? rate)
    {
        if (!rate.HasValue || double.IsNaN(rate.Value) || double.IsInfinity(rate.Value))
        {
            return null;
        }

        return Math.Round(rate.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
