using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика контекста пользователя бота
/// </summary>
public interface IBotUserContextService
{
    /// <summary>
    /// Проверяет, что пользователь с в БД
    /// </summary>
    Task<bool> IsCurrentUserTelegramAsync(
        Guid userId,
        long telegramUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает текущий контекст (ChildId, LastActivityAt) пользователя бота.
    /// </summary>
    Task<BotUserContext?> GetAsync(
        long telegramUserId,
        bool bumpLastActivity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт или обновляет контекст пользователя бота
    /// </summary>
    Task<BotUserContext> UpsertAsync(
        long telegramUserId,
        Guid? childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает пользователя по Telegram ID
    /// </summary>
    Task<User?> FindUserByTelegramIdAsync(
        long telegramUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список привязанных детей пользователя
    /// </summary>
    Task<IReadOnlyList<ChildSummaryBotDto>> GetLinkedChildrenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет существование ребёнка
    /// </summary>
    Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);
}
