// Интерфейс для разрешения конфликтов синхронизации
using SugarGuard.Junior.Models.Api;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Разрешитель конфликтов синхронизации с медицинской логикой
/// Requirements: 14.1, 14.2, 14.3, 14.4, 14.5
/// </summary>
public interface ISyncConflictResolver
{
    /// <summary>
    /// Разрешает конфликт между локальной и серверной версией данных
    /// </summary>
    /// <param name="conflictInfo">Информация о конфликте (из SugarGuard.Junior.Models.Api)</param>
    /// <param name="localData">JSON локальной версии данных</param>
    /// <returns>Результат разрешения конфликта</returns>
    Task<ConflictResolutionResult> ResolveConflictAsync(
        SyncConflictInfo conflictInfo,
        string localData);

    /// <summary>
    /// Очищает старую историю конфликтов
    /// </summary>
    /// <param name="daysToKeep">Количество дней для хранения истории (по умолчанию 30)</param>
    /// <returns>Количество удалённых записей</returns>
    Task<int> CleanupOldConflictHistoryAsync(int daysToKeep = 30);
}

/// <summary>
/// Результат разрешения конфликта
/// </summary>
public class ConflictResolutionResult
{
    public string WinningVersion { get; set; } = string.Empty; // "Server" или "Local"
    public bool ShouldUpdateLocal { get; set; }
    public string ResolvedData { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
