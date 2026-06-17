// Интерфейс для сервиса рюкзака
using SugarGuard.Junior.Database;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис для работы с рюкзаком
/// </summary>
public interface IBackpackService
{
    /// <summary>
    /// Получает все активные перекусы ребёнка
    /// </summary>
    Task<List<BackpackItem>> GetBackpackAsync(string childId);

    /// <summary>
    /// Добавляет перекус в рюкзак
    /// </summary>
    Task<BackpackItem?> AddSnackAsync(string childId, string snackName, double breadUnits);

    /// <summary>
    /// Удаляет перекус из рюкзака
    /// </summary>
    Task<bool> RemoveSnackAsync(string backpackItemId, string childId, string removedBy = "child");

    /// <summary>
    /// Очищает весь рюкзак
    /// </summary>
    Task<bool> ClearBackpackAsync(string childId);

    /// <summary>
    /// Получает статистику рюкзака
    /// </summary>
    Task<BackpackStatistics> GetStatisticsAsync(string childId);

    /// <summary>
    /// Получает историю перекусов
    /// </summary>
    Task<List<BackpackHistory>> GetHistoryAsync(string childId, DateTime startDate, DateTime endDate);
}

/// <summary>
/// Статистика рюкзака
/// </summary>
public class BackpackStatistics
{
    /// <summary>
    /// Количество перекусов в рюкзаке
    /// </summary>
    public int SnackCount { get; set; }

    /// <summary>
    /// Общее количество хлебных единиц
    /// </summary>
    public double TotalBreadUnits { get; set; }

    /// <summary>
    /// Время последнего обновления
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Перекусы, отсортированные по хлебным единицам
    /// </summary>
    public List<BackpackItem> Items { get; set; } = new();
}
