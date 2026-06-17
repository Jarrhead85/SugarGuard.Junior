namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на установку контекста пользователя Telegram-бота
/// </summary>
public class SetBotUserContextRequest
{
    public Guid? ChildId { get; set; }
}

/// <summary>
/// Ответ с контекстом пользователя Telegram-бота
/// </summary>
public class BotUserContextResponse
{
    public long TelegramUserId { get; set; }
    public Guid? CurrentChildId { get; set; }
    public bool HasContext { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

/// <summary>
/// Ответ со списком привязанных детей
/// </summary>
public class LinkedChildrenResponse
{
    public long TelegramUserId { get; set; }
    public List<ChildSummaryBotDto> Children { get; set; } = new();
    public int TotalChildren { get; set; }
}

/// <summary>
/// Краткая информация о ребёнке для контекста Telegram-бота
/// </summary>
public class ChildSummaryBotDto
{
    public Guid ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string DiabetesType { get; set; } = string.Empty;
}
