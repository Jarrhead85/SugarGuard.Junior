using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Заявка кандидата в врачи на проверку квалификации.
/// Документы хранятся в закрытом файловом хранилище и доступны только администрации.
/// </summary>
[Table("doctor_verification_requests")]
public sealed class DoctorVerificationRequest
{
    [Key]
    [Column("request_id")]
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required, MaxLength(200)]
    [Column("specialty")]
    public string Specialty { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    [Column("encrypted_license_number")]
    public string EncryptedLicenseNumber { get; set; } = string.Empty;

    [MaxLength(240)]
    [Column("organization_name")]
    public string? OrganizationName { get; set; }

    [MaxLength(2000)]
    [Column("comment")]
    public string? Comment { get; set; }

    [Column("status")]
    public DoctorVerificationStatus Status { get; set; } = DoctorVerificationStatus.Draft;

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by_user_id")]
    public Guid? ReviewedByUserId { get; set; }

    [MaxLength(1000)]
    [Column("review_comment")]
    public string? ReviewComment { get; set; }

    public User User { get; set; } = null!;
    public ICollection<DoctorVerificationDocument> Documents { get; set; } = new List<DoctorVerificationDocument>();
}
