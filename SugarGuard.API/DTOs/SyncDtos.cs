using SugarGuard.Shared.Constants;

namespace SugarGuard.API.DTOs;

public class SyncMeasurementItemRequest
{
    public Guid ChildId { get; set; }
    public decimal GlucoseValue { get; set; }
    public DateTime MeasurementTime { get; set; }
    public string? ChildState { get; set; }
    public string? Notes { get; set; }
    public string? DataSource { get; set; }
    public string? ClientOperationId { get; set; }
}

public class SyncMeasurementsRequest
{
    public List<SyncMeasurementItemRequest> Measurements { get; set; } = [];
    public string? AppVersion { get; set; }
    public DateTime? LastSyncTime { get; set; }
}

public class SyncConflictDto
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = "Measurement";
    public DateTime ServerModifiedAt { get; set; }
    public DateTime LocalModifiedAt { get; set; }
    public string? ServerVersion { get; set; }
    public string WinningVersion { get; set; } = "Server";
    public string ResolutionStrategy { get; set; } = SyncResolutionStrategy.ServerWinsOnDuplicate;
}

public class SyncMeasurementsResponse
{
    public bool Success { get; set; }
    public int SyncedCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SyncConflictDto> Conflicts { get; set; } = [];
}

public class SyncLogResponse
{
    public Guid SyncLogId { get; set; }
    public Guid ChildId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsConflict { get; set; }
    public string? Error { get; set; }
    public string? ResolutionSource { get; set; }

    public DateTime CreatedAt { get; set; }
}
