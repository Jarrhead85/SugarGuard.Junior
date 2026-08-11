namespace SugarGuard.Junior.Models.Sensors;

/// <summary>
/// Результат сохранения показания, пришедшего от датчика.
/// </summary>
public sealed record SensorMeasurementSaveResult(
    bool IsSaved,
    bool IsDuplicate,
    string? MeasurementId,
    string? ErrorMessage,
    bool RequiresConfirmation = false);

/// <summary>
/// Latest unauthenticated Android broadcast waiting for an explicit local decision.
/// The identifier prevents confirming a newer value than the one shown in the UI.
/// </summary>
public sealed record PendingSensorGlucoseReading(
    string ConfirmationId,
    string ChildId,
    SugarGuard.Junior.Core.Sensors.SensorGlucoseReading Reading);
