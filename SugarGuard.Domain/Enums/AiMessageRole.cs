namespace SugarGuard.Domain.Enums;

/// <summary>
/// Роль сообщения в AI-конверсации.
/// </summary>
public enum AiMessageRole
{
    /// <summary>
    /// Сообщение пользователя.
    /// </summary>
    User = 0,

    /// <summary>
    /// Ответ AI-консультанта.
    /// </summary>
    Assistant = 1,

    /// <summary>
    /// Системное техническое сообщение.
    /// </summary>
    System = 2
}
