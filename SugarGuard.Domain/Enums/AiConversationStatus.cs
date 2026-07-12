namespace SugarGuard.Domain.Enums;

/// <summary>
/// Статус AI-конверсации ребёнка.
/// </summary>
public enum AiConversationStatus
{
    /// <summary>
    /// Активная конверсация, используемая для новых запросов.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Архивная конверсация, не используемая для новых запросов.
    /// </summary>
    Archived = 1
}
