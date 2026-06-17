using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Событие онбординга пользователя
/// </summary>
[Table("onboardingevents")]
public class OnboardingEvent
{   
    [Key]
    [Column("onboardingeventid")]
    public Guid OnboardingEventId { get; set; } = Guid.NewGuid(); // PK события
       
    [Column("userid")]
    public Guid UserId { get; set; } // ID пользователя
   
    [Column("stepnumber")]
    public int StepNumber { get; set; } // Номер шага
   
    [Column("stepname")]
    [MaxLength(64)]
    public string StepName { get; set; } = string.Empty; // название шага
   
    [Column("eventtype")]
    [MaxLength(16)]
    public string EventType { get; set; } = string.Empty; // Тип события
   
    [Column("userrole")]
    [MaxLength(32)]
    public string UserRole { get; set; } = string.Empty; // Роль пользователя на момент события
   
    [Column("durationonsecond")]
    public int? DurationOnSecond { get; set; } // Продолжительность нахождения на шаге в секундах
   
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; } // Метаданные события
   
    [Column("requestip")]
    [MaxLength(45)]
    public string? RequestIp { get; set; } // IP адрес запроса
   
    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Время создания

    //Навигационные свойства
    
    public User? User { get; set; } // Навигация - пользователь
}
