// Интерфейс репозитория для User
using SugarGuard.Junior.Models.Core;

namespace SugarGuard.Junior.Repositories.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с пользователями (родителями)
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Получает пользователя по email
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Получает пользователя по Telegram ID
    /// </summary>
    Task<User?> GetByTelegramIdAsync(long telegramUserId);

    /// <summary>
    /// Получает расшифрованное полное имя пользователя
    /// </summary>
    Task<string> GetFullNameAsync(User user);

    /// <summary>
    /// Получает расшифрованное имя пользователя
    /// </summary>
    Task<string> GetFirstNameAsync(User user);

    /// <summary>
    /// Получает расшифрованную фамилию пользователя
    /// </summary>
    Task<string> GetLastNameAsync(User user);

    /// <summary>
    /// Получает расшифрованный email пользователя
    /// </summary>
    Task<string> GetEmailAsync(User user);

    /// <summary>
    /// Получает расшифрованный номер телефона пользователя
    /// </summary>
    Task<string> GetPhoneNumberAsync(User user);

    /// <summary>
    /// Добавляет пользователя с шифрованием персональных данных
    /// </summary>
    Task<User> AddUserWithEncryptionAsync(User user);

    /// <summary>
    /// Обновляет пользователя с шифрованием персональных данных
    /// </summary>
    Task<User> UpdateUserWithEncryptionAsync(User user);
}