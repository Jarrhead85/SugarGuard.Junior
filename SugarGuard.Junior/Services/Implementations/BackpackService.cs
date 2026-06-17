// Реализация сервиса рюкзака
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<BackpackService> _logger;
    private readonly IBackpackRepository _backpackRepository;
    private readonly ISyncService _syncService;

    public BackpackService(
        ILogger<BackpackService> logger,
        IBackpackRepository backpackRepository,
        ISyncService syncService)
    {
        _logger = logger;
        _backpackRepository = backpackRepository;
        _syncService = syncService;
    }

    /// <summary>
    /// Получает все активные перекусы ребёнка
    /// </summary>
    public async Task<List<BackpackItem>> GetBackpackAsync(string childId)
    {
        try
        {
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
                ChildId = childId,
                SnackName = snackName,
                Carbs = breadUnits,
                AddedBy = "child"
            };

            await _syncService.QueueItemAsync(
                item.BackpackItemId,
                "BackpackItem",
                "Insert",
                JsonConvert.SerializeObject(request));

            _logger.LogInformation(" Перекус добавлен: {SnackName}", snackName);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при добавлении перекуса: {Message}", ex.Message);
            throw;
        }
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
                await _syncService.QueueItemAsync(backpackItemId, "BackpackItem", "Delete",
                    JsonConvert.SerializeObject(removeRequest));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении перекуса: {Message}", ex.Message);
            throw;
        }
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

            await Task.WhenAll(items.Select(item =>
            {
                var removeRequest = new RemoveSnackRequest
                {
                    SnackId = item.BackpackItemId,
                    ChildId = item.ChildId,
                    RemovedBy = "child"
                };
                return _syncService.QueueItemAsync(item.BackpackItemId, "BackpackItem", "Delete",
                    JsonConvert.SerializeObject(removeRequest));
            }));

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
