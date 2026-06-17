namespace SugarGuard.Bot.Services;

/// <summary>
/// Интерфейс сервиса для управления контекстом пользователя Telegram-бота
/// </summary>
public interface IBotUserContextService
{
    /// <summary>
    /// Получить текущий ChildId для пользователя бота
    /// </summary>
    /// <param name="telegramUserId">Telegram ID пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>ChildId или null если не установлен</returns>
    Task<Guid?> GetCurrentChildIdAsync(long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Установить текущий ChildId для пользователя бота
    /// </summary>
    /// <param name="telegramUserId">Telegram ID пользователя</param>
    /// <param name="childId">ID ребёнка для установки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если успешно установлен</returns>
    Task<bool> SetCurrentChildIdAsync(long telegramUserId, Guid? childId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить список привязанных детей для пользователя бота
    /// </summary>
    /// <param name="telegramUserId">Telegram ID пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список привязанных детей</returns>
    Task<List<ChildSummaryBot>> GetLinkedChildrenAsync(long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очистить контекст пользователя (сбросить текущий ChildId)
    /// </summary>
    /// <param name="telegramUserId">Telegram ID пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если успешно очищен</returns>
    Task<bool> ClearContextAsync(long telegramUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Краткая информация о ребёнке для бота
/// </summary>
public class ChildSummaryBot
{
    public Guid ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string DiabetesType { get; set; } = string.Empty;
}
