using SugarGuard.Junior.Core.Sensors;
using SugarGuard.Junior.Models.Sensors;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Принимает данные от внешнего источника глюкозы и связывает их с активным профилем ребёнка.
/// </summary>
public interface ISensorGlucoseIngestionService
{
    /// <summary>
    /// Сохраняет показание в локальной базе и ставит его в очередь синхронизации.
    /// </summary>
    Task<SensorMeasurementSaveResult> IngestAsync(SensorGlucoseReading reading);

    /// <summary>Returns the latest unauthenticated broadcast awaiting a local decision.</summary>
    Task<PendingSensorGlucoseReading?> GetPendingAsync();

    /// <summary>
    /// Confirms exactly the reading previously displayed to the local user, then
    /// allows it to enter the normal measurement and medical-notification flow.
    /// </summary>
    Task<SensorMeasurementSaveResult> ConfirmPendingAsync(string confirmationId);

    /// <summary>Discards exactly the pending reading previously displayed to the user.</summary>
    Task<bool> RejectPendingAsync(string confirmationId);
}
