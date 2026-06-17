using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для работы с геолокацией устройства
/// Использует Microsoft.Maui.Essentials для получения координат
/// </summary>
public class LocationService : ILocationService
{
    private readonly ILogger<LocationService> _logger;
    private readonly IApiClient _apiClient;

    // Настройки по умолчанию для получения геолокации
    private static readonly GeolocationRequest DefaultRequest = new()
    {
        DesiredAccuracy = GeolocationAccuracy.Medium,
        Timeout = TimeSpan.FromSeconds(10)
    };

    public LocationService(ILogger<LocationService> logger, IApiClient apiClient)
    {
        _logger = logger;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Получает текущие координаты устройства
    /// </summary>
    public async Task<Interfaces.Location?> GetCurrentLocationAsync(TimeSpan? timeout = null)
    {
        try
        {
            _logger.LogInformation("Запрос текущей геолокации");

            // Проверяем разрешения
            var hasPermission = await IsLocationPermissionGrantedAsync();
            if (!hasPermission)
            {
                _logger.LogWarning("Нет разрешения на доступ к геолокации");
                
                // Пытаемся запросить разрешение
                var permissionGranted = await RequestLocationPermissionAsync();
                if (!permissionGranted)
                {
                    _logger.LogError("Не удалось получить разрешение на геолокацию");
                    return null;
                }
            }

            // Настраиваем запрос с таймаутом
            var request = new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = timeout ?? TimeSpan.FromSeconds(10)
            };

            // Получаем координаты
            var systemLocation = await Geolocation.GetLocationAsync(request);
            
            if (systemLocation == null)
            {
                _logger.LogWarning("Не удалось получить координаты");
                return null;
            }

            var location = new Interfaces.Location
            {
                Latitude = systemLocation.Latitude,
                Longitude = systemLocation.Longitude,
                Accuracy = systemLocation.Accuracy,
                Timestamp = systemLocation.Timestamp.DateTime
            };

            // Пытаемся получить адрес (необязательно)
            try
            {
                var placemarks = await Geocoding.GetPlacemarksAsync(systemLocation.Latitude, systemLocation.Longitude);
                var placemark = placemarks?.FirstOrDefault();
                
                if (placemark != null)
                {
                    location.Address = $"{placemark.Thoroughfare} {placemark.SubThoroughfare}, {placemark.Locality}".Trim(' ', ',');
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось получить адрес по координатам");
                // Продолжаем без адреса
            }

            _logger.LogInformation("Получены координаты: {Lat:F6}, {Lon:F6} (точность: {Accuracy:F0}м)", 
                location.Latitude, location.Longitude, location.Accuracy ?? 0);

            return location;
        }
        catch (FeatureNotSupportedException ex)
        {
            _logger.LogError(ex, "Геолокация не поддерживается на этом устройстве");
            return null;
        }
        catch (FeatureNotEnabledException ex)
        {
            _logger.LogError(ex, "Геолокация отключена на устройстве");
            return null;
        }
        catch (PermissionException ex)
        {
            _logger.LogError(ex, "Нет разрешения на доступ к геолокации");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении геолокации");
            return null;
        }
    }

    /// <summary>
    /// Отправляет координаты на сервер при критическом уровне глюкозы (через IApiClient).
    /// </summary>
    public async Task<bool> SendLocationToParentsAsync(string childId, double criticalGlucose, Interfaces.Location location)
    {
        try
        {
            _logger.LogWarning("Отправка критического уведомления с геолокацией для {ChildId}", childId);

            var request = new Models.Api.CriticalAlertRequest
            {
                ChildId = childId,
                GlucoseValue = criticalGlucose,
                MeasurementTime = DateTime.UtcNow,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Address = location.Address
            };

            var success = await _apiClient.SendCriticalAlertAsync(request);
            if (success)
            {
                _logger.LogInformation("Критическое уведомление с геолокацией отправлено");
            }
            else
            {
                _logger.LogError("Ошибка при отправке критического уведомления");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при отправке критического уведомления с геолокацией");
            return false;
        }
    }

    /// <summary>
    /// Проверяет, разрешён ли доступ к геолокации
    /// </summary>
    public async Task<bool> IsLocationPermissionGrantedAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке разрешений геолокации");
            return false;
        }
    }

    /// <summary>
    /// Запрашивает разрешение на доступ к геолокации
    /// </summary>
    public async Task<bool> RequestLocationPermissionAsync()
    {
        try
        {
            _logger.LogInformation("Запрос разрешения на геолокацию");

            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            
            var granted = status == PermissionStatus.Granted;
            
            if (granted)
            {
                _logger.LogInformation("Разрешение на геолокацию получено");
            }
            else
            {
                _logger.LogWarning("Разрешение на геолокацию отклонено: {Status}", status);
            }

            return granted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при запросе разрешения на геолокацию");
            return false;
        }
    }
}