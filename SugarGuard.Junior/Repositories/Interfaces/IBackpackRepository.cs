// Интерфейс для работы с рюкзаком
using SugarGuard.Junior.Database;

namespace SugarGuard.Junior.Repositories.Interfaces;

/// <summary>
/// Репозиторий для работы с рюкзаком (активные перекусы)
/// </summary>
public interface IBackpackRepository : IRepository<BackpackItem>
{
    /// <summary>
    /// Получает все активные перекусы ребёнка
    /// </summary>
    Task<List<BackpackItem>> GetByChildIdAsync(string childId, bool includeChild = false);

    /// <summary>
    /// Добавляет перекус в рюкзак
    /// </summary>
    Task<BackpackItem> AddSnackAsync(string childId, string snackName, double carbs);

    /// <summary>
    /// Создаёт или обновляет локальный элемент, пришедший с сервера.
    /// </summary>
    Task UpsertSyncedSnackAsync(string backpackItemId, string childId, string snackName, double breadUnits, DateTime createdAt);

    /// <summary>
    /// Помечает локальный элемент как синхронизированный.
    /// </summary>
    Task<bool> MarkAsSyncedAsync(string backpackItemId);

    /// <summary>
    /// Удаляет локальные синхронизированные элементы, которых больше нет на сервере.
    /// </summary>
    Task<int> RemoveSyncedItemsMissingFromServerAsync(string childId, IReadOnlySet<string> serverItemIds);

    /// <summary>
    /// Удаляет один локальный дубль старого формата, у которого нет операции
    /// добавления в очереди. Используется после подтверждённого потребления.
    /// </summary>
    Task<bool> RemoveOrphanedUnsyncedDuplicateAsync(string childId, string snackName, double breadUnits);

    /// <summary>
    /// Удаляет перекус из рюкзака и добавляет в историю
    /// </summary>
    Task<bool> RemoveSnackAsync(string backpackItemId, string childId, string removedBy);

    /// <summary>
    /// Атомарно переносит перекус из рюкзака в журнал употребления.
    /// </summary>
    Task<bool> ConsumeSnackAsync(string backpackItemId, string childId, DateTime consumedAt);

    /// <summary>
    /// Получает историю всех перекусов
    /// </summary>
    Task<List<BackpackHistory>> GetHistoryAsync(string childId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Очищает весь рюкзак ребёнка
    /// </summary>
    Task<int> ClearBackpackAsync(string childId);

    /// <summary>
    /// Получает общее количество углеводов в рюкзаке
    /// </summary>
    Task<double> GetTotalCarbsAsync(string childId);

    /// <summary>
    /// Получает сумму хлебных единиц, потреблённых сегодня (из SnackConsumptionLog).
    /// </summary>
    Task<double> GetConsumedBreadUnitsTodayAsync(string childId);

    /// <summary>
    /// Получает расшифрованное название перекуса
    /// </summary>
    Task<string> GetDecryptedSnackNameAsync(BackpackItem item);

    /// <summary>
    /// Получает расшифрованное название перекуса из истории
    /// </summary>
    Task<string> GetDecryptedSnackNameAsync(BackpackHistory item);

    /// <summary>
    /// Получает расшифрованные хлебные единицы
    /// </summary>
    Task<double> GetDecryptedBreadUnitsAsync(BackpackItem item);

    /// <summary>
    /// Получает расшифрованные хлебные единицы из истории
    /// </summary>
    Task<double> GetDecryptedBreadUnitsAsync(BackpackHistory item);

    /// <summary>
    /// Шифрует хлебные единицы перед сохранением
    /// </summary>
    Task<string> EncryptBreadUnitsAsync(double breadUnits);
}
