namespace SugarGuard.Junior.Core.Sensors;

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
    double? RateMmolPerLiterPerMinute);
