// Реализация репозитория для User
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;

namespace SugarGuard.Junior.Repositories.Implementations;

/// <summary>
/// Репозиторий для работы с пользователями (родителями)
/// Обеспечивает шифрование персональных данных
/// </summary>
public class UserRepository : BaseRepository<User>, IUserRepository
{
    private readonly ICryptoService _cryptoService;

    public UserRepository(IDbContextFactory<AppDbContext> factory, ILogger<UserRepository> logger, ICryptoService cryptoService)
        : base(factory, logger)
    {
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Получает пользователя по зашифрованному email (read-only с AsNoTracking для оптимизации)
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        try
        {
            // Шифруем email для поиска
            var encryptedEmail = await _cryptoService.EncryptAsync(email);
            
            await using var ctx = await CreateDbContextAsync();
            var user = await ctx.Set<User>()
                .Where(u => u.EncryptedEmail == encryptedEmail)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (user != null)
            {
                Logger.LogDebug(" Пользователь найден по email");
            }

            return user;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при поиске пользователя по email: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает пользователя по Telegram ID (read-only с AsNoTracking для оптимизации)
    /// </summary>
    public async Task<User?> GetByTelegramIdAsync(long telegramUserId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var user = await ctx.Set<User>()
                .Where(u => u.TelegramUserId == telegramUserId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (user != null)
            {
                Logger.LogDebug(" Пользователь найден по Telegram ID");
            }

            return user;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при поиске пользователя по Telegram ID: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует персональные данные пользователя перед сохранением
    /// </summary>
    private async Task EncryptUserDataAsync(User user)
    {
        try
        {
            // Проверяем, не зашифрованы ли уже данные (Base64 содержит '=')
            if (!string.IsNullOrEmpty(user.EncryptedFirstName) && !user.EncryptedFirstName.Contains('='))
            {
                user.EncryptedFirstName = await _cryptoService.EncryptAsync(user.EncryptedFirstName);
            }

            if (!string.IsNullOrEmpty(user.EncryptedLastName) && !user.EncryptedLastName.Contains('='))
            {
                user.EncryptedLastName = await _cryptoService.EncryptAsync(user.EncryptedLastName);
            }

            if (!string.IsNullOrEmpty(user.EncryptedEmail) && !user.EncryptedEmail.Contains('='))
            {
                user.EncryptedEmail = await _cryptoService.EncryptAsync(user.EncryptedEmail);
            }

            if (!string.IsNullOrEmpty(user.EncryptedPhoneNumber) && !user.EncryptedPhoneNumber.Contains('='))
            {
                user.EncryptedPhoneNumber = await _cryptoService.EncryptAsync(user.EncryptedPhoneNumber);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании данных пользователя: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает расшифрованное полное имя пользователя
    /// </summary>
    public async Task<string> GetFullNameAsync(User user)
    {
        try
        {
            var firstName = await _cryptoService.DecryptAsync(user.EncryptedFirstName);
            var lastName = await _cryptoService.DecryptAsync(user.EncryptedLastName);
            return $"{firstName} {lastName}";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении полного имени пользователя: {Message}", ex.Message);
            return "*** ОШИБКА ДЕШИФРОВАНИЯ ***";
        }
    }

    /// <summary>
    /// Получает расшифрованное имя пользователя
    /// </summary>
    public async Task<string> GetFirstNameAsync(User user)
    {
        try
        {
            return await _cryptoService.DecryptAsync(user.EncryptedFirstName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении имени пользователя: {Message}", ex.Message);
            return "*** ОШИБКА ***";
        }
    }

    /// <summary>
    /// Получает расшифрованную фамилию пользователя
    /// </summary>
    public async Task<string> GetLastNameAsync(User user)
    {
        try
        {
            return await _cryptoService.DecryptAsync(user.EncryptedLastName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении фамилии пользователя: {Message}", ex.Message);
            return "*** ОШИБКА ***";
        }
    }

    /// <summary>
    /// Получает расшифрованный email пользователя
    /// </summary>
    public async Task<string> GetEmailAsync(User user)
    {
        try
        {
            return await _cryptoService.DecryptAsync(user.EncryptedEmail);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении email пользователя: {Message}", ex.Message);
            return "*** ОШИБКА ***";
        }
    }

    /// <summary>
    /// Получает расшифрованный номер телефона пользователя
    /// </summary>
    public async Task<string> GetPhoneNumberAsync(User user)
    {
        try
        {
            if (string.IsNullOrEmpty(user.EncryptedPhoneNumber))
                return string.Empty;
                
            return await _cryptoService.DecryptAsync(user.EncryptedPhoneNumber);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении номера телефона пользователя: {Message}", ex.Message);
            return "*** ОШИБКА ***";
        }
    }

    /// <summary>
    /// Добавляет пользователя с шифрованием персональных данных
    /// </summary>
    public async Task<User> AddUserWithEncryptionAsync(User user)
    {
        await EncryptUserDataAsync(user);
        return await AddAsync(user);
    }

    /// <summary>
    /// Обновляет пользователя с шифрованием персональных данных
    /// </summary>
    public async Task<User> UpdateUserWithEncryptionAsync(User user)
    {
        await EncryptUserDataAsync(user);
        return await UpdateAsync(user);
    }
}