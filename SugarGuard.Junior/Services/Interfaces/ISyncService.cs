// Интерфейс для сервиса синхронизации
namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис синхронизации
/// Отвечает за offlineонлайн синхронизацию данных
/// </summary>
public interface ISyncService : IDisposable
{
    /// <summary>
    /// Инициализирует сервис синхронизации
    /// Запускает фоновую проверку соединения
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Проверяет наличие интернета
    /// </summary>
    Task<bool> IsConnectedAsync();

    /// <summary>
    /// Запускает синхронизацию вручную
    /// </summary>
    Task<bool> SyncNowAsync();

    /// <summary>
    /// Добавляет запись в очередь синхронизации
    /// </summary>
    Task<bool> QueueItemAsync(string entityId, string entityType, string operationType, string payload);

    /// <summary>
    /// Получает статус синхронизации
    /// </summary>
    Task<SyncStatus> GetStatusAsync();

    /// <summary>
    /// Событие: когда синхронизация начинается
    /// </summary>
    event EventHandler<SyncStartedEventArgs>? SyncStarted;

    /// <summary>
    /// Событие: когда синхронизация завершается
    /// </summary>
    event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    /// <summary>
    /// Событие: когда статус соединения меняется
    /// </summary>
    event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

    /// <summary>
    /// Демонстрационный метод для тестирования разрешения конфликтов
    /// </summary>
    Task<bool> TestConflictResolutionAsync();

    /// <summary>
    /// Получает статистику конфликтов синхронизации
    /// </summary>
    Task<ConflictStatistics> GetConflictStatisticsAsync();
}

/// <summary>
/// Статус синхронизации
/// </summary>
public class SyncStatus
{
    /// <summary>
    /// Статус соединения
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Количество ожидающих синхронизации записей
    /// </summary>
    public int PendingItemsCount { get; set; }

    /// <summary>
    /// Последняя успешная синхронизация
    /// </summary>
    public DateTime? LastSuccessfulSync { get; set; }

    /// <summary>
    /// Идёт ли синхронизация сейчас
    /// </summary>
    public bool IsSyncing { get; set; }

    /// <summary>
    /// Процент завершённости (0-100)
    /// </summary>
    public int ProgressPercent { get; set; }
}

/// <summary>
/// Событие: синхронизация начинается
/// </summary>
public class SyncStartedEventArgs : EventArgs
{
    public int ItemsCount { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Событие: синхронизация завершается
/// </summary>
public class SyncCompletedEventArgs : EventArgs
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool IsSuccessful { get; set; }
}

/// <summary>
/// Событие: связь с интернетом изменилась
/// </summary>
public class ConnectivityChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>
/// Статистика конфликтов синхронизации
/// </summary>
public class ConflictStatistics
{
    public int TotalConflicts { get; set; }
    public int RecentConflicts { get; set; }
    public int ServerWins { get; set; }
    public int LocalWins { get; set; }
    public DateTime? LastConflictAt { get; set; }
}
