// Реализация сервиса локального хранилища
// Использует встроённый MAUI SecureStorage
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

public class StorageService : IStorageService
{
    /// <summary>
    /// Логгер для записи событий
    /// </summary>
    private readonly ILogger<StorageService> _logger;

    /// <summary>
    /// Конструктор (DI автоматически передаст логгер)
    /// </summary>
    public StorageService(ILogger<StorageService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Сохраняет значение в защищённое хранилище MAUI
    /// MAUI SecureStorage автоматически шифрует данные по платформе:
    /// - iOS: Keychain
    /// - Android: Keystore
    /// - Windows: CredentialManager
    /// </summary>
    public async Task<bool> SaveAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);
            _logger.LogInformation("Значение сохранено: {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Получает значение из защищённого хранилища
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);
            if (value != null)
            {
                _logger.LogInformation("Значение получено: {Key}", key);
            }
            else
            {
                _logger.LogWarning("Ключ не найден: {Key}", key);
            }
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении {Key}: {Message}", key, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Удаляет значение из хранилища
    /// </summary>
    public Task<bool> DeleteAsync(string key)
    {
        try
        {
            SecureStorage.Remove(key);
            _logger.LogInformation("Значение удалено: {Key}", key);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении {Key}: {Message}", key, ex.Message);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Проверяет наличие ключа
    /// </summary>
    public async Task<bool> ContainsKeyAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Очищает всё хранилище
    /// Используется при выходе из аккаунта
    /// </summary>
    public Task<bool> ClearAsync()
    {
        try
        {
            SecureStorage.RemoveAll();
            _logger.LogInformation("Хранилище очищено");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке хранилища: {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }
}
