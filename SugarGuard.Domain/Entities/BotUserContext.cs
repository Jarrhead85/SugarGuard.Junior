using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

/// <summary>
/// Модель контекста пользователя Telegram-бота
/// </summary>
[Table("bot_user_contexts")]
public class BotUserContext
{
    [Key]
    [Column("context_id")]
    public Guid ContextId { get; set; } = Guid.NewGuid();

    [Column("telegram_user_id")]
    [Required]
    public long TelegramUserId { get; set; }

    [Column("current_child_id")]
    public Guid? CurrentChildId { get; set; }

    [Column("last_activity_at")]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационное свойство
    [ForeignKey(nameof(CurrentChildId))]
    public virtual Child? CurrentChild { get; set; }
}
