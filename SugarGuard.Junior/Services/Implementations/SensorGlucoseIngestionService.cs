using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Sensors;
using SugarGuard.Junior.Models.Sensors;
using SugarGuard.Junior.Services.Interfaces;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Привязывает входящий поток CGM к текущему профилю ребёнка.
/// Данные не отбрасываются из-за отсутствия сети: сохранение всегда начинается с локальной базы.
/// </summary>
public sealed class SensorGlucoseIngestionService : ISensorGlucoseIngestionService
{
    private readonly IStorageService _storageService;
    private readonly IMeasurementService _measurementService;
    private readonly ILogger<SensorGlucoseIngestionService> _logger;

    public SensorGlucoseIngestionService(
        IStorageService storageService,
        IMeasurementService measurementService,
        ILogger<SensorGlucoseIngestionService> logger)
    {
        _storageService = storageService;
        _measurementService = measurementService;
        _logger = logger;
    }

    public async Task<SensorMeasurementSaveResult> IngestAsync(SensorGlucoseReading reading)
    {
        var childId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
        if (string.IsNullOrWhiteSpace(childId))
        {
            _logger.LogWarning("Показание {Source} не сохранено: текущий профиль ребёнка не выбран.", reading.Source);
            return new SensorMeasurementSaveResult(false, false, null, "Не выбран профиль ребёнка.");
        }

        return await _measurementService.ProcessSensorMeasurementAsync(childId, reading);
    }
}
