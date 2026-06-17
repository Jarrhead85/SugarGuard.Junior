// Сервис для безопасного хранения чувствительных данных
// (токены аутентификации, ключи и т.д.)
// 
// Использует встроённое защищённое хранилище MAUI:
// - iOS: Keychain
// - Android: Keystore
// - Windows: CredentialManager
using Microsoft.Extensions.Logging;

namespace SugarGuard.Junior.Security;

public class SecureStorageService(ILogger<SecureStorageService> logger) : ISecureStorageService
{
    /// <summary>
    /// Префиксы для разных типов данных (для организации)
    /// </summary>
    private const string TokenPrefix = "token_";
    private const string CredentialPrefix = "cred_";
    private const string KeyPrefix = "key_";

    /// <summary>
    /// Сохраняет токен аутентификации (JWT)
    /// Токены - самые критичные данные, нужно защищать особенно тщательно
    /// </summary>
    public async Task<bool> SaveAuthTokenAsync(string accessToken, string? refreshToken = null)
    {
        try
        {
            // Сохраняем access token
            await SecureStorage.SetAsync(
                TokenPrefix + "access",
                accessToken);

            logger.LogInformation(" Access token сохранён");

            // Сохраняем refresh token если предоставлен
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await SecureStorage.SetAsync(
                    TokenPrefix + "refresh",
                    refreshToken);

                logger.LogInformation(" Refresh token сохранён");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при сохранении токенов: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Получает access token
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(TokenPrefix + "access");
            if (token != null)
            {
                logger.LogInformation(" Access token получен");
            }
            else
            {
                logger.LogWarning(" Access token не найден");
            }
            return token;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при получении access token: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Получает refresh token
    /// </summary>
    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(TokenPrefix + "refresh");
            if (token != null)
            {
                logger.LogInformation(" Refresh token получен");
            }
            else
            {
                logger.LogWarning(" Refresh token не найден");
            }
            return token;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при получении refresh token: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Удаляет оба токена (при выходе из приложения)
    /// </summary>
    public bool ClearAuthTokens()
    {
        try
        {
            SecureStorage.Remove(TokenPrefix + "access");
            SecureStorage.Remove(TokenPrefix + "refresh");
            logger.LogInformation(" Токены удалены");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при удалении токенов: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Проверяет, сохранён ли токен
    /// </summary>
    public async Task<bool> HasAuthTokenAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(TokenPrefix + "access");
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Сохраняет общие данные в защищённое хранилище
    /// (ключи, ID пользователя и т.д.)
    /// </summary>
    public async Task<bool> SaveAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(KeyPrefix + key, value);
            logger.LogInformation(" Данные '{Key}' сохранены", key);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при сохранении '{Key}': {Message}", key, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Получает данные из защищённого хранилища
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(KeyPrefix + key);
            if (value != null)
            {
                logger.LogInformation(" Данные '{Key}' получены", key);
            }
            else
            {
                logger.LogWarning(" Данные '{Key}' не найдены", key);
            }
            return value;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при получении '{Key}': {Message}", key, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Удаляет данные из защищённого хранилища
    /// </summary>
    public bool Delete(string key)
    {
        try
        {
            SecureStorage.Remove(KeyPrefix + key);
            logger.LogInformation(" Данные '{Key}' удалены", key);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при удалении '{Key}': {Message}", key, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Проверяет, существуют ли данные с ключом
    /// </summary>
    public async Task<bool> ContainsKeyAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(KeyPrefix + key);
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Очищает всё защищённое хранилище
    /// ОСТОРОЖНО! Это удалит ВСЕ данные, включая токены
    /// </summary>
    public bool ClearAll()
    {
        try
        {
            SecureStorage.RemoveAll();
            logger.LogWarning(" Защищённое хранилище полностью очищено");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при очистке хранилища: {Message}", ex.Message);
            return false;
        }
    }
}

/// <summary>
/// Интерфейс для защищённого хранилища
/// </summary>
public interface ISecureStorageService
{
    Task<bool> SaveAuthTokenAsync(string accessToken, string? refreshToken = null);
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    bool ClearAuthTokens();
    Task<bool> HasAuthTokenAsync();
    Task<bool> SaveAsync(string key, string value);
    Task<string?> GetAsync(string key);
    bool Delete(string key);
    Task<bool> ContainsKeyAsync(string key);
    bool ClearAll();
}
