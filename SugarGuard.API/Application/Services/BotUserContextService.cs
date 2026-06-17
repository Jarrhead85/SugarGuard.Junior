using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация контекста пользователя бота
/// </summary>
public class BotUserContextService : IBotUserContextService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<BotUserContextService> _logger;

    public BotUserContextService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<BotUserContextService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsCurrentUserTelegramAsync(
        Guid userId,
        long telegramUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Загружаем только TelegramId — не весь User
        var telegramId = await db.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => (long?)u.TelegramId)
            .FirstOrDefaultAsync(cancellationToken);

        return telegramId.HasValue && telegramId.Value == telegramUserId;
    }

    /// <inheritdoc/>
    public async Task<BotUserContext?> GetAsync(
        long telegramUserId,
        bool bumpLastActivity,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var botContext = await db.BotUserContexts
            .FirstOrDefaultAsync(buc => buc.TelegramUserId == telegramUserId, cancellationToken);

        if (botContext is null)
            return null;

        if (bumpLastActivity)
        {
            botContext.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return botContext;
    }

    /// <inheritdoc/>
    public async Task<BotUserContext> UpsertAsync(
        long telegramUserId,
        Guid? childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var botContext = await db.BotUserContexts
            .FirstOrDefaultAsync(buc => buc.TelegramUserId == telegramUserId, cancellationToken);

        var now = DateTime.UtcNow;

        if (botContext is null)
        {
            botContext = new BotUserContext
            {
                TelegramUserId = telegramUserId,
                CurrentChildId = childId,
                LastActivityAt = now,
                CreatedAt = now
            };
            db.BotUserContexts.Add(botContext);
            _logger.LogInformation(
                "BotUserContext создан: TelegramUserId={TelegramUserId} ChildId={ChildId}.",
                telegramUserId, childId);
        }
        else
        {
            botContext.CurrentChildId = childId;
            botContext.LastActivityAt = now;
            _logger.LogInformation(
                "BotUserContext обновлён: TelegramUserId={TelegramUserId} ChildId={ChildId}.",
                telegramUserId, childId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return botContext;
    }

    /// <inheritdoc/>
    public async Task<User?> FindUserByTelegramIdAsync(
        long telegramUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramId == telegramUserId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChildSummaryBotDto>> GetLinkedChildrenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.ParentChildLinks
            .AsNoTracking()
            .Where(pcl => pcl.ParentUserId == userId)
            .Select(pcl => new ChildSummaryBotDto
            {
                ChildId = pcl.Child.ChildId,
                FirstName = pcl.Child.FirstName,
                LastName = pcl.Child.LastName,
                DateOfBirth = pcl.Child.DateOfBirth,
                DiabetesType = pcl.Child.DiabetesType
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Children
            .AsNoTracking()
            .AnyAsync(c => c.ChildId == childId, cancellationToken);
    }
}
