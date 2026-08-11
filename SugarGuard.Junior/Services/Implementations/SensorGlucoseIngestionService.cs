using System.Text.Json;
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
    private const string PendingReadingStorageKey = "cgm_untrusted_reading_pending_v1";
    private const string PendingNotificationStorageKey = "cgm_untrusted_notification_utc";
    private static readonly TimeSpan PendingNotificationInterval = TimeSpan.FromMinutes(15);

    private readonly ICgmConnectionService _connectionService;
    private readonly IMeasurementService _measurementService;
    private readonly IStorageService _storageService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SensorGlucoseIngestionService> _logger;

    public SensorGlucoseIngestionService(
        ICgmConnectionService connectionService,
        IMeasurementService measurementService,
        IStorageService storageService,
        INotificationService notificationService,
        ILogger<SensorGlucoseIngestionService> logger)
    {
        _connectionService = connectionService;
        _measurementService = measurementService;
        _storageService = storageService;
        _notificationService = notificationService;
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

        if (!SensorGlucoseTrustPolicy.IsTrustedForMedicalUse(reading))
        {
            var pending = new PendingSensorGlucoseReading(
                Guid.NewGuid().ToString("N"),
                childId,
                reading with { Trust = SensorReadingTrust.UntrustedExternalBroadcast });

            if (!await _storageService.SaveAsync(PendingReadingStorageKey, JsonSerializer.Serialize(pending)))
            {
                return new SensorMeasurementSaveResult(
                    false,
                    false,
                    null,
                    "Не удалось безопасно сохранить показание для подтверждения.");
            }

            await _connectionService.MarkReadingReceivedAsync(reading.ReceivedAtUtc);
            await NotifyConfirmationRequiredAsync();

            _logger.LogInformation(
                "Получено неподтверждённое показание внешнего CGM-bridge; медицинская обработка приостановлена.");
            return new SensorMeasurementSaveResult(
                false,
                false,
                null,
                null,
                RequiresConfirmation: true);
        }

        var result = await _measurementService.ProcessSensorMeasurementAsync(childId, reading);
        if (result.IsSaved || result.IsDuplicate)
        {
            await _connectionService.MarkReadingReceivedAsync(reading.ReceivedAtUtc);
        }

        return result;
    }

    public async Task<PendingSensorGlucoseReading?> GetPendingAsync()
    {
        var serialized = await _storageService.GetAsync(PendingReadingStorageKey);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingSensorGlucoseReading>(serialized);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Повреждённое ожидающее показание CGM удалено.");
            await _storageService.DeleteAsync(PendingReadingStorageKey);
            return null;
        }
    }

    public async Task<SensorMeasurementSaveResult> ConfirmPendingAsync(string confirmationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationId);

        var pending = await GetPendingAsync();
        if (pending is null || !FixedTimeEquals(pending.ConfirmationId, confirmationId))
        {
            return new SensorMeasurementSaveResult(false, false, null, "Показание изменилось. Проверьте новое значение.");
        }

        var connection = await _connectionService.GetStatusAsync();
        if (!connection.IsConnected ||
            !string.Equals(connection.ChildId, pending.ChildId, StringComparison.Ordinal))
        {
            return new SensorMeasurementSaveResult(false, false, null, "Профиль CGM изменился. Подключите датчик заново.");
        }

        if (!SensorGlucoseTrustPolicy.TryConfirmLocally(
                pending.Reading,
                DateTime.UtcNow,
                out var confirmedReading,
                out var error) ||
            confirmedReading is null)
        {
            await _storageService.DeleteAsync(PendingReadingStorageKey);
            return new SensorMeasurementSaveResult(false, false, null, error);
        }

        var result = await _measurementService.ProcessSensorMeasurementAsync(pending.ChildId, confirmedReading);
        if (result.IsSaved || result.IsDuplicate)
        {
            await _storageService.DeleteAsync(PendingReadingStorageKey);
        }

        return result;
    }

    public async Task<bool> RejectPendingAsync(string confirmationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationId);

        var pending = await GetPendingAsync();
        return pending is not null &&
               FixedTimeEquals(pending.ConfirmationId, confirmationId) &&
               await _storageService.DeleteAsync(PendingReadingStorageKey);
    }

    private async Task NotifyConfirmationRequiredAsync()
    {
        var lastRaw = await _storageService.GetAsync(PendingNotificationStorageKey);
        if (DateTime.TryParse(
                lastRaw,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var last) &&
            last.ToUniversalTime() > DateTime.UtcNow.Subtract(PendingNotificationInterval))
        {
            return;
        }

        var sent = await _notificationService.SendLocalNotificationAsync(
            "Подтвердите данные датчика",
            "SugarGuard получил значение из внешнего приложения. Откройте настройки CGM и сверьте его перед использованием.",
            "cgm-confirm-reading");
        if (sent)
        {
            await _storageService.SaveAsync(PendingNotificationStorageKey, DateTime.UtcNow.ToString("O"));
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
