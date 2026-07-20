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
}
