using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

[Table("export_jobs")]
public class ExportJob
{
    [Key]
    [Column("export_job_id")]
    public Guid ExportJobId { get; set; } = Guid.NewGuid();

    [Column("requested_by_user_id")]
    public Guid RequestedByUserId { get; set; }

    [Column("child_id")]
    public Guid ChildId { get; set; }

    [Column("period_from")]
    public DateTime PeriodFrom { get; set; }

    [Column("period_to")]
    public DateTime PeriodTo { get; set; }

    [Column("format")]
    [MaxLength(32)]
    public string Format { get; set; } = "csv";

    [Column("status")]
    [MaxLength(32)]
    public string Status { get; set; } = "queued";

    [Column("download_url")]
    [MaxLength(1024)]
    public string? DownloadUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [NotMapped]
    public byte[]? CsvContent { get; set; }
}
