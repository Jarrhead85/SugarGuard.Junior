// Реализация сервиса рюкзака
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для работы с рюкзаком
/// </summary>
public class BackpackService : IBackpackService
{
    private const int MaxSyncRetries = 10;

    private readonly ILogger<BackpackService> _logger;
    private readonly IBackpackRepository _backpackRepository;
    private readonly ISyncService _syncService;
    private readonly IApiClient _apiClient;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public BackpackService(
        ILogger<BackpackService> logger,
        IBackpackRepository backpackRepository,
        ISyncService syncService,
        IApiClient apiClient,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _backpackRepository = backpackRepository;
        _syncService = syncService;
        _apiClient = apiClient;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Получает все активные перекусы ребёнка
    /// </summary>
    public async Task<List<BackpackItem>> GetBackpackAsync(string childId)
    {
        try
        {
            await PullServerBackpackAsync(childId);

            var items = await _backpackRepository.GetByChildIdAsync(childId);
            _logger.LogInformation(" Получен рюкзак: {ItemsCount} перекусов", items.Count);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при получении рюкзака: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Добавляет перекус в рюкзак
    /// </summary>
    public async Task<BackpackItem?> AddSnackAsync(string childId, string snackName, double breadUnits)
    {
        try
        {
            _logger.LogInformation(" Добавление перекуса: {SnackName} ({BreadUnits} ХЕ)", snackName, breadUnits);

            if (string.IsNullOrWhiteSpace(snackName))
            {
                _logger.LogWarning(" Название перекуса не может быть пустым");
                return null;
            }

            if (breadUnits <= 0)
            {
                _logger.LogWarning(" Хлебные единицы должны быть > 0");
                return null;
            }

            // Добавляем в БД
            var item = await _backpackRepository.AddSnackAsync(childId, snackName, breadUnits);

            // Добавляем в очередь синхронизации
            var request = new Models.Api.AddSnackRequest
            {
                BackpackItemId = item.BackpackItemId,
                ChildId = childId,
                SnackName = snackName,
                Carbs = breadUnits,
                AddedBy = "child"
            };

            try
            {
                await _syncService.QueueItemAsync(
                    item.BackpackItemId,
                    "BackpackItem",
                    "Insert",
                    JsonConvert.SerializeObject(request));
            }
            catch (Exception syncEx)
            {
                _logger.LogWarning(
                    syncEx,
                    "Перекус {BackpackItemId} сохранён локально, но не поставлен в очередь синхронизации",
                    item.BackpackItemId);
            }

            _logger.LogInformation(" Перекус добавлен: {SnackName}", snackName);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при добавлении перекуса: {Message}", ex.Message);
            throw;
        }
    }

    private async Task PullServerBackpackAsync(string childId)
    {
        try
        {
            var serverItems = await _apiClient.GetBackpackItemsAsync(childId);
            var serverIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingRemovalIds = await GetPendingRemovalIdsAsync(childId);

            foreach (var item in serverItems)
            {
                if (string.IsNullOrWhiteSpace(item.BackpackItemId))
                    continue;

                // Не возвращаем в UI запись, которую ребёнок уже удалил или съел
                // локально, пока соответствующая операция ожидает отправки.
                if (pendingRemovalIds.Contains(item.BackpackItemId))
                    continue;

                serverIds.Add(item.BackpackItemId);
                await _backpackRepository.UpsertSyncedSnackAsync(
                    item.BackpackItemId,
                    string.IsNullOrWhiteSpace(item.ChildId) ? childId : item.ChildId,
                    item.SnackName,
                    item.BreadUnits,
                    item.CreatedAt);
            }

            var removed = await _backpackRepository.RemoveSyncedItemsMissingFromServerAsync(childId, serverIds);

            _logger.LogInformation(
                "Рюкзак синхронизирован с сервером: {ServerCount} элементов, удалено устаревших {RemovedCount}",
                serverIds.Count,
                removed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось подтянуть рюкзак с сервера, показываем локальные данные");
        }
    }

    private async Task<HashSet<string>> GetPendingRemovalIdsAsync(string childId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var ids = await db.Set<SyncQueueItem>()
            .AsNoTracking()
            .Where(item => !item.IsSynced && item.RetryCount < MaxSyncRetries &&
                ((item.EntityType == "SnackConsumption") ||
                 (item.EntityType == "BackpackItem" && item.OperationType == SyncOperationType.Delete)))
            .Select(item => item.EntityId)
            .ToListAsync();

        return ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Удаляет перекус из рюкзака
    /// </summary>
    public async Task<bool> RemoveSnackAsync(string backpackItemId, string childId, string removedBy = "child")
    {
        try
        {
            _logger.LogInformation("Удаление перекуса: {BackpackItemId}", backpackItemId);

            var result = await _backpackRepository.RemoveSnackAsync(backpackItemId, childId, removedBy);

            if (result)
            {
                _logger.LogInformation("Перекус удалён");
                var removeRequest = new RemoveSnackRequest
                {
                    SnackId = backpackItemId,
                    ChildId = childId,
                    RemovedBy = removedBy
                };
                try
                {
                    await _syncService.QueueItemAsync(backpackItemId, "BackpackItem", "Delete",
                        JsonConvert.SerializeObject(removeRequest));
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(
                        syncEx,
                        "Удаление перекуса {BackpackItemId} сохранено локально, но не поставлено в очередь синхронизации",
                        backpackItemId);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении перекуса: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<bool> ConsumeSnackAsync(
        string backpackItemId,
        string childId,
        string snackName,
        double breadUnits,
        double currentGlucose)
    {
        var consumedAt = DateTime.UtcNow;
        var consumed = await _backpackRepository.ConsumeSnackAsync(
            backpackItemId,
            childId,
            consumedAt);

        if (!consumed)
            return false;

        var request = new ConsumeBackpackSnackRequest
        {
            BackpackItemId = backpackItemId,
            ChildId = childId,
            SnackName = snackName,
            BreadUnits = breadUnits,
            CurrentGlucose = currentGlucose,
            ConsumedAt = consumedAt
        };

        var queued = await _syncService.QueueItemAsync(
            backpackItemId,
            "SnackConsumption",
            "Insert",
            JsonConvert.SerializeObject(request));

        if (!queued)
        {
            _logger.LogError("Не удалось поставить употребление перекуса {BackpackItemId} в очередь", backpackItemId);
            return false;
        }

        // Очередь упорядочена по CreatedAt: если перекус ещё не успел попасть
        // на сервер, сначала выполнится Insert, затем Consume.
        await _syncService.SyncNowAsync();

        return true;
    }

    /// <summary>
    /// Очищает весь рюкзак
    /// </summary>
    public async Task<bool> ClearBackpackAsync(string childId)
    {
        try
        {
            _logger.LogInformation(" Очистка рюкзака");

            var items = await _backpackRepository.GetByChildIdAsync(childId);

            foreach (var item in items)
            {
                try
                {
                    var removeRequest = new RemoveSnackRequest
                    {
                        SnackId = item.BackpackItemId,
                        ChildId = item.ChildId,
                        RemovedBy = "child"
                    };
                    await _syncService.QueueItemAsync(item.BackpackItemId, "BackpackItem", "Delete",
                        JsonConvert.SerializeObject(removeRequest));
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(
                        syncEx,
                        "Очистка рюкзака: удаление {BackpackItemId} не поставлено в очередь синхронизации",
                        item.BackpackItemId);
                }
            }

            var count = await _backpackRepository.ClearBackpackAsync(childId);
            _logger.LogInformation(" Рюкзак очищен ({Count} перекусов удалено)", count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при очистке рюкзака: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает статистику рюкзака
    /// </summary>
    public async Task<BackpackStatistics> GetStatisticsAsync(string childId)
    {
        try
        {
            var items = await _backpackRepository.GetByChildIdAsync(childId);

            var breadUnitsTasks = items.Select(item => _backpackRepository.GetDecryptedBreadUnitsAsync(item));
            var breadUnitsResults = await Task.WhenAll(breadUnitsTasks);
            var itemsWithDecryptedBreadUnits = items.Zip(breadUnitsResults, (item, bu) => (item, breadUnits: bu)).ToList();
            var totalBreadUnits = breadUnitsResults.Sum();

            var sortedItems = itemsWithDecryptedBreadUnits
                .OrderByDescending(x => x.breadUnits)
                .Select(x => x.item)
                .ToList();

            var stats = new BackpackStatistics
            {
                SnackCount = items.Count,
                TotalBreadUnits = totalBreadUnits,
                LastUpdated = DateTime.UtcNow,
                Items = sortedItems
            };

            _logger.LogInformation(" Статистика: {SnackCount} перекусов, {TotalBreadUnits} ХЕ", stats.SnackCount, stats.TotalBreadUnits);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при получении статистики: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает историю перекусов
    /// </summary>
    public async Task<List<BackpackHistory>> GetHistoryAsync(string childId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var history = await _backpackRepository.GetHistoryAsync(childId, startDate, endDate);
            _logger.LogInformation(" История получена: {HistoryCount} записей", history.Count);
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при получении истории: {Message}", ex.Message);
            throw;
        }
    }
}
