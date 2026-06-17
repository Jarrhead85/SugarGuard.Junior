namespace SugarGuard.Infrastructure.Sync;

public class SyncMeasurement
{
    public Guid MeasurementId { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public decimal GlucoseValue { get; set; }
    public DateTime MeasuredAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SyncLogEntry
{
    public Guid SyncLogId { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public Guid MeasurementId { get; set; }
    public bool IsConflict { get; set; }
    public string? ConflictReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
