namespace SugarGuard.API.DTOs;

public class CreateExportJobRequest
{
    public Guid ChildId { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string Format { get; set; } = "csv";
}

public class ExportJobResponse
{
    public Guid ExportJobId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid ChildId { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string Format { get; set; } = "csv";
    public string Status { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
