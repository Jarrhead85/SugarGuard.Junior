using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities;

[Table("meal_schedules")]
public sealed class MealSchedule
{
    [Key]
    [Column("meal_schedule_id")]
    public Guid MealScheduleId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    public Guid ChildId { get; set; }

    [Column("meal_type")]
    public MealType MealType { get; set; }

    [Column("title")]
    [MaxLength(80)]
    public string Title { get; set; } = string.Empty;

    [Column("scheduled_time")]
    public TimeOnly ScheduledTime { get; set; }

    [Column("planned_bread_units", TypeName = "decimal(5,2)")]
    public decimal? PlannedBreadUnits { get; set; }

    [Column("days_of_week_mask")]
    public int DaysOfWeekMask { get; set; } = 127;

    [Column("reminder_enabled")]
    public bool ReminderEnabled { get; set; } = true;

    [Column("reminder_minutes_before")]
    public int ReminderMinutesBefore { get; set; } = 10;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ChildId))]
    public Child Child { get; set; } = null!;
}
