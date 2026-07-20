using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Модель пользователя в системе
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Column("telegram_id")]
    public long? TelegramId { get; set; }

    [Column("encrypted_first_name")]
    [MaxLength(500)]
    public string? EncryptedFirstName { get; set; }

    [Column("encrypted_last_name")]
    [MaxLength(500)]
    public string? EncryptedLastName { get; set; }

    [Column("profile_photo_url")]
    [MaxLength(512)]
    public string? ProfilePhotoUrl { get; set; }

    [Column("doctor_specialty")]
    [MaxLength(200)]
    public string? DoctorSpecialty { get; set; }

    [Column("encrypted_doctor_license")]
    [MaxLength(500)]
    public string? EncryptedDoctorLicense { get; set; }

    [Column("encrypted_email")]
    [MaxLength(500)]
    public string? EncryptedEmail { get; set; }

    [Column("password_hash")]
    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    [Column("password_salt")]
    [MaxLength(500)]
    public string? PasswordSalt { get; set; }

    [Column("email_for_login")]
    [MaxLength(256)]
    public string? EmailForLogin { get; set; } // Нормализованный email для входа

    [Column("role")]
    public UserRole Role { get; set; } = UserRole.Parent;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
   
    [Column("isactive")]
    public bool IsActive { get; set; } = true; // Активен ли пользователь
   
    [Column("deactivatedat")]
    public DateTime? DeactivatedAt { get; set; } // Дата и время деактивации
   
    [Column("onboardingcompleted")]
    public bool OnboardingCompleted { get; set; } = true; // Завершён ли онбординг
   
    [Column("onboardingcompletedat")]
    public DateTime? OnboardingCompletedAt { get; set; } // Дата и время завершения онбординга 
   
    [Column("onboardingcurrentstep")]
    public int OnboardingCurrentStep { get; set; } = 0; // Текущий шаг онбординга
   
    [Column("onboardingstartedat")]
    public DateTime? OnboardingStartedAt { get; set; } // Дата и время начала онбординга
   
    [Column("onboardingskippedat")]
    public DateTime? OnboardingSkippedAt { get; set; } // Дата и время пропуска онбординга
   
    [Column("isemailverified")]
    public bool IsEmailVerified { get; set; } = false; // Признак подтверждения email пользователя
   
    [Column("emailverifiedat")]
    public DateTime? EmailVerifiedAt { get; set; } // ата подтверждения email

    [Column("map_provider")]
    [MaxLength(32)]
    public string MapProvider { get; set; } = "yandex";

    [Column("daily_summary_enabled")]
    public bool DailySummaryEnabled { get; set; } = true;
   
    public virtual ICollection<ParentChildLink> ParentChildLinks { get; set; } = new List<ParentChildLink>(); // Навигационные свойства
}
