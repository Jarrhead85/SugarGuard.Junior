using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Связь между пользователем-врачом и ребёнком
/// </summary>
[Table("doctor_child_links")]
public sealed class DoctorChildLink
{
    [Column("link_id")]
    public Guid LinkId { get; set; } // Уникальный идентификатор связи 

    [Column("doctor_user_id")]
    public Guid DoctorUserId { get; set; } // Идентификатор пользователя-врача

    [Column("child_id")]
    public Guid ChildId { get; set; } // Идентификатор ребёнка

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } // Дата и время создания связи

    [Column("isactive")]
    public bool IsActive { get; set; } = true; // Активна ли связь

    [Column("deactivatedat")]
    public DateTime? DeactivatedAt { get; set; } // UTC-дата деактивации связ

    [Column("linkedbyuserid")]
    public Guid? LinkedByUserId { get; set; } // ID пользователя, создавшего связь

    [Column("notes")]
    [MaxLength(1000)]
    public string? Notes { get; set; } // Административная заметка к связи

    public User DoctorUser { get; set; } = null!; // Пользователь-врач, которому принадлежит эта связь

    public Child Child { get; set; } = null!; // Ребёнок, к данным которого врач имеет доступ
}
