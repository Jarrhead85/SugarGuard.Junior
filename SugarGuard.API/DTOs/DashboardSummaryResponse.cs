namespace SugarGuard.API.DTOs;

public class DashboardSummaryResponse
{
    public Guid ChildId { get; set; }
    public decimal? LatestGlucose { get; set; }
    public DateTime? LatestMeasurementTime { get; set; }
    public string? LatestGlucoseStatus { get; set; }
    public string? LatestGlucoseUiState { get; set; }
    public int TotalMeasurements { get; set; }
    public int CriticalEvents { get; set; }
    public int RecommendationsCount { get; set; }
    public int PendingExportJobs { get; set; }
    public int PendingSyncConflicts { get; set; }
}

public class DashboardHistoryItemResponse
{
    public Guid MeasurementId { get; set; }
    public DateTime MeasurementTime { get; set; }
    public decimal GlucoseValue { get; set; }
    public string GlucoseStatus { get; set; } = string.Empty;
    public string GlucoseUiState { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string? Notes { get; set; }
}
