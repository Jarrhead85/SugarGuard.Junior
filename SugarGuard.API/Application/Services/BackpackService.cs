using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация сервиса рюкзака
/// </summary>
public class BackpackService : IBackpackService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<BackpackService> _logger;

    public BackpackService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit,
        IChildAccessService childAccess,
        ILogger<BackpackService> logger)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _childAccess = childAccess;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BackpackResponse?> GetAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var items = await db.BackpackItems
            .AsNoTracking()
            .Where(bi => bi.ChildId == childId)
            .OrderBy(bi => bi.CreatedAt)
            .Select(bi => new BackpackItemResponse
            {
                BackpackItemId = bi.BackpackItemId,
                ChildId = bi.ChildId,
                SnackName = bi.SnackName,
                BreadUnits = bi.BreadUnits,
                AddedBy = bi.AddedBy,
                CreatedAt = bi.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new BackpackResponse
        {
            ChildId = childId,
            Items = items,
            TotalItems = items.Count,
            TotalBreadUnits = items.Sum(i => i.BreadUnits),
            LastUpdated = items.Count > 0 ? items.Max(i => i.CreatedAt) : DateTime.UtcNow
        };
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

    /// <inheritdoc/>
    public async Task<BackpackItemResponse> AddAsync(
        CreateBackpackItemRequest request,
        Guid addedByUserId,
        CancellationToken cancellationToken = default)
    {
        var item = new BackpackItem
        {
            ChildId = request.ChildId,
            SnackName = request.SnackName.Trim(),
            BreadUnits = request.BreadUnits,
            AddedBy = $"userId:{addedByUserId}",
            CreatedAt = DateTime.UtcNow
        };

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.BackpackItems.Add(item);
            await db.SaveChangesAsync(cancellationToken);
        }

        await _audit.WriteAsync(
            action: "backpack.item_added",
            targetType: "BackpackItem",
            targetId: item.BackpackItemId.ToString(),
            details: $"Child={item.ChildId};" +
                     $"Snack={item.SnackName};" +
                     $"BreadUnits={item.BreadUnits}",
            cancellationToken: CancellationToken.None);

        _logger.LogInformation(
            "AddAsync: SnackName={SnackName} BreadUnits={BreadUnits} ChildId={ChildId} Actor={ActorId}.",
            item.SnackName, item.BreadUnits, item.ChildId, addedByUserId);

        return new BackpackItemResponse
        {
            BackpackItemId = item.BackpackItemId,
            ChildId = item.ChildId,
            SnackName = item.SnackName,
            BreadUnits = item.BreadUnits,
            AddedBy = item.AddedBy,
            CreatedAt = item.CreatedAt
        };
    }

    /// <inheritdoc/>
    public async Task<BackpackRemoveResult> RemoveAsync(
        Guid itemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.BackpackItems
            .FirstOrDefaultAsync(bi => bi.BackpackItemId == itemId, cancellationToken);

        if (item is null)
        {
            _logger.LogWarning("RemoveAsync: перекус не найден. ItemId={ItemId}.", itemId);
            return BackpackRemoveResult.NotFound;
        }

        if (!await _childAccess.CanAccessChildAsync(item.ChildId, cancellationToken))
        {
            _logger.LogWarning(
                "RemoveAsync: доступ запрещён. ItemId={ItemId} ChildId={ChildId} " +
                "CurrentUserId={CurrentUserId}.",
                itemId, item.ChildId, currentUserId);
            return BackpackRemoveResult.Forbidden;
        }

        var deletedAt = DateTime.UtcNow;
        var historyRecord = new BackpackHistory
        {
            ChildId = item.ChildId,
            SnackName = item.SnackName,
            BreadUnits = item.BreadUnits,
            AddedAt = item.CreatedAt,
            DeletedAt = deletedAt,
            DeletedBy = BackpackHistoryActor.RemovedByUser(currentUserId),
            CreatedAt = deletedAt
        };

        db.BackpackHistory.Add(historyRecord);
        db.BackpackItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "backpack.item_removed",
            targetType: "BackpackItem",
            targetId: itemId.ToString(),
            details: $"Child={item.ChildId};" +
                     $"Snack={item.SnackName};" +
                     $"BreadUnits={item.BreadUnits}",
            cancellationToken: CancellationToken.None);

        _logger.LogInformation(
            "RemoveAsync: SnackName={SnackName} ChildId={ChildId} Actor={ActorId}.",
            item.SnackName, item.ChildId, currentUserId);

        return BackpackRemoveResult.Removed;
    }

    /// <inheritdoc/>
    public async Task<BackpackItem?> GetByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.BackpackItems
            .AsNoTracking()
            .FirstOrDefaultAsync(bi => bi.BackpackItemId == itemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<BackpackConsumeOutcome> ConsumeAsync(
        Guid itemId,
        Guid consumedByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.BackpackItems
            .FirstOrDefaultAsync(bi => bi.BackpackItemId == itemId, cancellationToken);

        if (item is null)
        {
            _logger.LogWarning("ConsumeAsync: перекус не найден. ItemId={ItemId}.", itemId);
            return new BackpackConsumeOutcome(BackpackConsumeResultStatus.NotFound, null);
        }

        if (!await _childAccess.CanAccessChildAsync(item.ChildId, cancellationToken))
        {
            _logger.LogWarning(
                "ConsumeAsync: доступ запрещён. ItemId={ItemId} ChildId={ChildId} " +
                "CurrentUserId={CurrentUserId}.",
                itemId, item.ChildId, consumedByUserId);
            return new BackpackConsumeOutcome(BackpackConsumeResultStatus.Forbidden, null);
        }

        var consumedAt = DateTime.UtcNow;

        var consumptionLog = new SnackConsumptionLog
        {
            ChildId = item.ChildId,
            SnackName = item.SnackName,
            BreadUnits = item.BreadUnits,
            ConsumedAt = consumedAt,
            CreatedAt = consumedAt
        };

        var historyRecord = new BackpackHistory
        {
            ChildId = item.ChildId,
            SnackName = item.SnackName,
            BreadUnits = item.BreadUnits,
            AddedAt = item.CreatedAt,
            DeletedAt = consumedAt,
            DeletedBy = BackpackHistoryActor.ConsumedByUser(consumedByUserId),
            CreatedAt = consumedAt
        };

        db.SnackConsumptionLogs.Add(consumptionLog);
        db.BackpackHistory.Add(historyRecord);
        db.BackpackItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "backpack.item_consumed",
            targetType: "SnackConsumptionLog",
            targetId: consumptionLog.LogId.ToString(),
            details: $"Child={item.ChildId};" +
                     $"Snack={item.SnackName};" +
                     $"BreadUnits={item.BreadUnits}",
            cancellationToken: CancellationToken.None);

        _logger.LogInformation(
            "ConsumeAsync: SnackName={SnackName} BreadUnits={BreadUnits} ChildId={ChildId} Actor={ActorId}.",
            item.SnackName, item.BreadUnits, item.ChildId, consumedByUserId);

        return new BackpackConsumeOutcome(
            BackpackConsumeResultStatus.Consumed,
            new ConsumeSnackResult(item.ChildId, item.SnackName, item.BreadUnits, consumedAt));
    }
}
