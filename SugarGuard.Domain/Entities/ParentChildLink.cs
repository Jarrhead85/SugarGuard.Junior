using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Связь между пользователем-родителем и ребёнком
/// </summary>
[Table("parent_child_links")]
public sealed class ParentChildLink
{
    // Первичный ключ
    [Column("link_id")]
    public Guid LinkId { get; set; } // Уникальный идентификатор связи

    // Внешние ключи
    [Column("parent_user_id")]
    public Guid ParentUserId { get; set; } // Идентификатор пользователя-родителя 

    [Column("child_id")]
    public Guid ChildId { get; set; } // Идентификатор ребёнка

    // Поля данных
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } // Дата и время создания связи
   
    [Column("linkedbyuserid")]
    public Guid? LinkedByUserId { get; set; } // ID пользователя, создавшего связь
   
    [Column("notes")]
    [MaxLength(1000)]
    public string? Notes { get; set; } // Административная заметка к связи

    // Навигационные свойства 
    public User ParentUser { get; set; } = null!; // Пользователь-родитель, которому принадлежит эта связь

    public Child Child { get; set; } = null!; // Ребёнок, к которому привязан родитель
}
