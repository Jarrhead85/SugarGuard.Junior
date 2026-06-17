using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Расписание измерений глюкозы
/// </summary>
[Table("measurement_schedules")]
public class MeasurementSchedule
{
    [Key]
    [Column("schedule_id")]
    public Guid ScheduleId { get; set; } = Guid.NewGuid();

    [Column("child_id")]
    [Required]
    public Guid ChildId { get; set; }

    [Column("scheduled_time")]
    [Required]
    public TimeOnly ScheduledTime { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey(nameof(ChildId))]
    public virtual Child Child { get; set; } = null!;
}
