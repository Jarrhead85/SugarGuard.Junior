using Microsoft.EntityFrameworkCore;

namespace SugarGuard.Infrastructure.Sync;

/// <summary>
/// Разрешает конфликты синхронизации измерений
/// </summary>
public class SyncConflictResolver
{
    private readonly SyncDbContext _db;

    public SyncConflictResolver(SyncDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Отправляет измерение на синхронизацию
    /// </summary>
    public async Task<SyncLogEntry> SubmitAsync(SyncMeasurement incoming)
    {
        var existing = await _db.Measurements
            .FirstOrDefaultAsync(m =>
                m.ChildId == incoming.ChildId &&
                m.MeasuredAt == incoming.MeasuredAt);

        SyncLogEntry logEntry;

        if (existing is not null)
        {
            logEntry = new SyncLogEntry
            {
                ChildId = incoming.ChildId,
                MeasurementId = existing.MeasurementId,
                IsConflict = true,
                ConflictReason =
                    $"Duplicate measurement for ChildId={incoming.ChildId} " +
                    $"at MeasuredAt={incoming.MeasuredAt:O}. " +
                    $"Retained existing MeasurementId={existing.MeasurementId}."
            };
        }
        else
        {
            _db.Measurements.Add(incoming);

            logEntry = new SyncLogEntry
            {
                ChildId = incoming.ChildId,
                MeasurementId = incoming.MeasurementId,
                IsConflict = false
            };
        }

        _db.SyncLogs.Add(logEntry);
        await _db.SaveChangesAsync();

        return logEntry;
    }
}
