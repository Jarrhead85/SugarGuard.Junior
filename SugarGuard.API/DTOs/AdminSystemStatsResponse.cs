namespace SugarGuard.API.DTOs;

public class AdminSystemStatsResponse
{
    public int HangfireActiveJobs { get; set; }
    public int TotalUsers { get; set; }
    public int TotalChildren { get; set; }
    public long TotalMeasurements { get; set; }
    public int PendingSyncItems { get; set; }
    public int UnresolvedConflicts { get; set; }
    public int PendingExportJobs { get; set; }
    public int CompletedExportJobsToday { get; set; }
    public DateTime ServerUtcTime { get; set; }
}
