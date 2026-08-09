using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Sensors;
using SugarGuard.Junior.Models.Sensors;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Привязывает входящий поток CGM к текущему профилю ребёнка.
/// Данные не отбрасываются из-за отсутствия сети: сохранение всегда начинается с локальной базы.
/// </summary>
public sealed class SensorGlucoseIngestionService : ISensorGlucoseIngestionService
{
    private readonly ICgmConnectionService _connectionService;
    private readonly IMeasurementService _measurementService;
    private readonly ILogger<SensorGlucoseIngestionService> _logger;

    public SensorGlucoseIngestionService(
        ICgmConnectionService connectionService,
        IMeasurementService measurementService,
        ILogger<SensorGlucoseIngestionService> logger)
    {
        _connectionService = connectionService;
        _measurementService = measurementService;
        _logger = logger;
    }

    public async Task<SensorMeasurementSaveResult> IngestAsync(SensorGlucoseReading reading)
    {
        var connection = await _connectionService.GetStatusAsync();
        var childId = connection.ChildId;
        if (!connection.IsConnected || string.IsNullOrWhiteSpace(childId))
        {
            _logger.LogWarning("Показание {Source} не сохранено: текущий профиль ребёнка не выбран.", reading.Source);
            return new SensorMeasurementSaveResult(false, false, null, "Не выбран профиль ребёнка.");
        }

        var result = await _measurementService.ProcessSensorMeasurementAsync(childId, reading);
        if (result.IsSaved || result.IsDuplicate)
        {
            await _connectionService.MarkReadingReceivedAsync(reading.ReceivedAtUtc);
        }

        return result;
    }
}
