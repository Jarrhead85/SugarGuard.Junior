namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Модель геолокации
/// </summary>
public class Location
{
    /// <summary>
    /// Широта
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Долгота
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Точность в метрах
    /// </summary>
    public double? Accuracy { get; set; }

    /// <summary>
    /// Адрес (если удалось определить)
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Время получения координат
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Сервис для работы с геолокацией устройства
/// Получает текущие координаты и отправляет их на сервер при критических состояниях
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Получает текущие координаты устройства
    /// </summary>
    /// <param name="timeout">Таймаут получения координат (по умолчанию 10 секунд)</param>
    /// <returns>Координаты или null если не удалось получить</returns>
    Task<Location?> GetCurrentLocationAsync(TimeSpan? timeout = null);

    /// <summary>
    /// Отправляет координаты на сервер при критическом уровне глюкозы
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="criticalGlucose">Критическое значение глюкозы</param>
    /// <param name="location">Координаты устройства</param>
    /// <returns>true если успешно отправлено</returns>
    Task<bool> SendLocationToParentsAsync(string childId, double criticalGlucose, Location location);

    /// <summary>
    /// Проверяет, разрешён ли доступ к геолокации
    /// </summary>
    /// <returns>true если разрешён доступ</returns>
    Task<bool> IsLocationPermissionGrantedAsync();

    /// <summary>
    /// Запрашивает разрешение на доступ к геолокации
    /// </summary>
    /// <returns>true если разрешение получено</returns>
    Task<bool> RequestLocationPermissionAsync();
}