using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Метаданные закрытого документа, приложенного к заявке врача.
/// </summary>
[Table("doctor_verification_documents")]
public sealed class DoctorVerificationDocument
{
    [Key]
    [Column("document_id")]
    public Guid DocumentId { get; set; } = Guid.NewGuid();

    [Column("request_id")]
    public Guid RequestId { get; set; }

    [Required, MaxLength(240)]
    [Column("original_file_name")]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    [Column("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    [Column("stored_file_name")]
    public string StoredFileName { get; set; } = string.Empty;

    [Column("size_bytes")]
    public long SizeBytes { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DoctorVerificationRequest Request { get; set; } = null!;
}
