// Сервис для безопасного хеширования паролей
// Использует PBKDF2-SHA256 с 10,000 итерациями
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace SugarGuard.Junior.Security;

/// <summary>
/// Сервис для хеширования и проверки паролей
/// 
/// Почему PBKDF2, а не bcrypt/scrypt?
/// - PBKDF2: встроен в .NET, стандарт, используется везде
/// - Bcrypt: сложнее в .NET MAUI
/// - Scrypt: ещё сложнее
/// 
/// 10,000 итераций:
/// - Замедляет атаки brute-force
/// - Рекомендация NIST (2023): минимум 600,000, но MAUI может быть медленнее
/// - Компромисс: безопасность + скорость на мобильных устройствах
/// </summary>
public class PasswordHashingService : IPasswordHashingService
{
    private readonly ILogger<PasswordHashingService> _logger;

    /// <summary>
    /// Количество итераций PBKDF2 (NIST SP 800-132 рекомендует минимум 600 000 для PBKDF2-SHA256).
    /// </summary>
    private const int Iterations = 600_000;

    /// <summary>
    /// Размер хеша (в байтах)
    /// 32 байта = 256 бит = SHA256
    /// </summary>
    private const int HashSize = 32;

    public PasswordHashingService(ILogger<PasswordHashingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Хеширует пароль с помощью PBKDF2-SHA256
    /// 
    /// Процесс:
    /// 1. Берём пароль и соль
    /// 2. Применяем PBKDF2 10,000 раз
    /// 3. Возвращаем хеш (больше нельзя восстановить пароль)
    /// 
    /// Пример:
    ///   Пароль:     "MyP@ssw0rd"
    ///   Соль:       "j3k4l5..." (случайная)
    ///   Результат:  "hash123..." (32 байта)
    /// </summary>
    public string HashPassword(string password, string salt)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
            {
                _logger.LogError(" Пароль не может быть пустым");
                throw new ArgumentException("Пароль не может быть пустым", nameof(password));
            }

            if (string.IsNullOrEmpty(salt))
            {
                _logger.LogError(" Соль не может быть пустой");
                throw new ArgumentException("Соль не может быть пустой", nameof(salt));
            }

            // Преобразуем строки в байты
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var saltBytes = Convert.FromBase64String(salt);

            // Создаём PBKDF2 объект с SHA256
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password: password,
                salt: saltBytes,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256))
            {
                // Получаем хеш размером 32 байта
                var hashBytes = pbkdf2.GetBytes(HashSize);

                // Преобразуем в Base64 для удобства хранения
                var hashBase64 = Convert.ToBase64String(hashBytes);

                _logger.LogInformation(" Пароль успешно захеширован");
                return hashBase64;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при хешировании пароля");
            throw;
        }
    }

    /// <summary>
    /// Проверяет, совпадает ли пароль с хешем
    /// 
    /// Как это работает:
    /// 1. Берём введённый пароль
    /// 2. Хешируем его с той же солью
    /// 3. Сравниваем полученный хеш с сохранённым хешем
    /// 4. Если совпадают - пароль правильный!
    /// 
    /// Пример:
    ///   Пользователь вводит: "MyP@ssw0rd"
    ///   Мы хешируем: "hash123..."
    ///   Сравниваем с сохранённым: "hash123..."
    ///   Результат: ✅ ВЕРНО!
    /// </summary>
    public bool VerifyPassword(string password, string hash, string salt)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Попытка проверки пустого пароля");
                return false;
            }

            if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            {
                _logger.LogWarning("Хеш или соль не найдены");
                return false;
            }

            // Хешируем введённый пароль с той же солью
            var hashOfInput = HashPassword(password, salt);

            // Используем constant-time сравнение (защита от timing-атак)
            // Обычное сравнение может выдать информацию о том, 
            // какие символы правильные, а какие нет
            bool isValid = CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(hashOfInput),
                Convert.FromBase64String(hash));

            if (isValid)
            {
                _logger.LogInformation(" Пароль верный");
            }
            else
            {
                _logger.LogWarning("Пароль неверный (попытка входа)");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке пароля: {Message}", ex.Message);
            return false;
        }
    }
}

/// <summary>
/// Интерфейс для сервиса хеширования паролей
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>
    /// Хеширует пароль с помощью PBKDF2-SHA256
    /// </summary>
    string HashPassword(string password, string salt);

    /// <summary>
    /// Проверяет, совпадает ли пароль с хешем
    /// </summary>
    bool VerifyPassword(string password, string hash, string salt);
}
