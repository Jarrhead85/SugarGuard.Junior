namespace SugarGuard.Junior.Models.Sensors;

/// <summary>
/// Результат сохранения показания, пришедшего от датчика.
/// </summary>
public sealed record SensorMeasurementSaveResult(
    bool IsSaved,
    bool IsDuplicate,
    string? MeasurementId,
    string? ErrorMessage);
