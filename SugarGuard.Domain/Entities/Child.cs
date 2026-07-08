using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Модель ребёнка с диабетом
/// </summary>
[Table("children")]
public class Child
{
    [Key]
    [Column("child_id")]
    public Guid ChildId { get; set; } = Guid.NewGuid();

    [Column("first_name")]
    [MaxLength(255)]
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    [MaxLength(255)]
    [Required]
    public string LastName { get; set; } = string.Empty;

    [Column("date_of_birth")]
    public DateOnly DateOfBirth { get; set; }

    [Column("weight", TypeName = "decimal(5,2)")]
    public decimal Weight { get; set; }

    [Column("height", TypeName = "decimal(5,2)")]
    public decimal Height { get; set; }

    [Column("diabetes_type")]
    [MaxLength(50)]
    [Required]
    public string DiabetesType { get; set; } = string.Empty;

    [Column("diagnosis_date")]
    public DateOnly? DiagnosisDate { get; set; }

    [Column("insulin_scheme")]
    [MaxLength(500)]
    public string? InsulinScheme { get; set; }

    [Column("current_insulins", TypeName = "jsonb")]
    public string CurrentInsulins { get; set; } = "[]";

    [Column("photo_url")]
    [MaxLength(500)]
    public string? PhotoUrl { get; set; } // URL фото профиля ребёнка

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("time_zone_id")]
    [MaxLength(100)]
    [Required]
    public string TimeZoneId { get; set; } = "UTC";
   
    [Column("setupcompleted")]
    public bool SetupCompleted { get; set; } = false; // Завершён ли первоначальный setup профиля ребёнка
   
    [Column("setupcompletedat")]
    public DateTime? SetupCompletedAt { get; set; } // UTC-дата завершения setup

    // Навигационные свойства
    public virtual DiabetesSettings? DiabetesSettings { get; set; }
    public virtual ICollection<ParentChildLink> ParentChildLinks { get; set; } = new List<ParentChildLink>();
    public virtual ICollection<DoctorChildLink> DoctorChildLinks { get; set; } = new List<DoctorChildLink>();
    public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    public virtual ICollection<BackpackItem> BackpackItems { get; set; } = new List<BackpackItem>();
    public virtual ICollection<BackpackHistory> BackpackHistory { get; set; } = new List<BackpackHistory>();
    public virtual ICollection<AIRecommendation> AIRecommendations { get; set; } = new List<AIRecommendation>();
    public virtual ICollection<MeasurementSchedule> MeasurementSchedules { get; set; } = new List<MeasurementSchedule>();
    public virtual ICollection<ConnectionCode> ConnectionCodes { get; set; } = new List<ConnectionCode>();
    public virtual ICollection<SnackConsumptionLog> SnackConsumptionLogs { get; set; } = new List<SnackConsumptionLog>();
    public virtual ICollection<NutritionEntry> NutritionEntries { get; set; } = new List<NutritionEntry>();
    public virtual ICollection<MealSchedule> MealSchedules { get; set; } = new List<MealSchedule>();
    public virtual ICollection<ChildAchievement> Achievements { get; set; } = new List<ChildAchievement>();
}
