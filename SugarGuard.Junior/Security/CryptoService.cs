// Сервис для шифрования/дешифрования данных
// Использует AES-256 с Android KeyStore для безопасного хранения ключей
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace SugarGuard.Junior.Security;

/// <summary>
/// Legacy-сервис криптографии на базе AES-256-CBC.
/// <para>
/// <b>DEPRECATED (2026-06-03):</b> заменён на <see cref="MauiEncryptionService"/>
/// поверх <see cref="Core.Security.AesGcmEncryptionService"/>. CBC не обеспечивает
/// аутентификацию ciphertext (padding oracle, bit-flipping), поэтому новые
/// версии SugarGuard используют AES-256-GCM.
/// </para>
/// <para>
/// Этот класс остаётся в проекте для обратной совместимости при чтении
/// legacy-CBC данных, но больше не регистрируется в DI. Все пути
/// шифрования/дешифрования идут через <see cref="MauiEncryptionService"/>,
/// который роутит чтение legacy-CBC через
/// <see cref="Core.Security.LegacyAesCbcDecryptionService"/>.
/// </para>
/// </summary>
[Obsolete("Заменён на MauiEncryptionService (AES-256-GCM). Удалить после v1.0.1.")]
public class CryptoService : ICryptoService
{
    private readonly ILogger<CryptoService> _logger;

    /// <summary>
    /// Константы для AES-256
    /// </summary>
    private const int KeySizeBytes = 32;    // 256 бит = 32 байта
    private const int IvSizeBytes = 16;     // 128 бит = 16 байт (для AES)
    private const int SaltSizeBytes = 16;   // 128 бит = 16 байт

    /// <summary>
    /// Префикс для ключа в защищённом хранилище
    /// </summary>
    private const string MasterKeyId = "sugarguard_master_key";

    public CryptoService(ILogger<CryptoService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Инициализирует криптографический сервис
    /// Должна быть вызвана при первом запуске приложения
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Инициализация криптографии...");

            // Проверяем, есть ли уже главный ключ
            var existingKey = await SecureStorage.GetAsync(MasterKeyId);
            if (existingKey != null)
            {
                _logger.LogInformation("Главный ключ уже существует");
                return true;
            }

            // Генерируем новый главный ключ (32 байта для AES-256)
            using (var rng = RandomNumberGenerator.Create())
            {
                var keyBytes = new byte[KeySizeBytes];
                rng.GetBytes(keyBytes);

                // Преобразуем в Base64 для хранения в текстовом виде
                var keyBase64 = Convert.ToBase64String(keyBytes);

                // Сохраняем в защищённое хранилище
                await SecureStorage.SetAsync(MasterKeyId, keyBase64);

                _logger.LogInformation("Главный ключ успешно сгенерирован и сохранён");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации криптографии");
            return false;
        }
    }

    /// <summary>
    /// Шифрует строку данных
    /// 
    /// Как это работает:
    /// 1. Генерируем случайный IV (Initialization Vector)
    /// 2. Используем AES-256 в режиме CBC с PKCS7 padding
    /// 3. Возвращаем: IV + зашифрованные_данные (оба в Base64)
    /// 
    /// Пример:
    ///   Входные данные: "Иван Петров"
    ///   Выходные данные: "base64_iv_here...base64_encrypted_here..."
    /// </summary>
    public async Task<string> EncryptAsync(string plainText)
    {
        try
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            // Получаем главный ключ
            var keyBase64 = await SecureStorage.GetAsync(MasterKeyId);
            if (keyBase64 == null)
            {
                _logger.LogError("Главный ключ не найден. Вызовите Initialize() сначала!");
                throw new InvalidOperationException("Криптография не инициализирована");
            }

            var keyBytes = Convert.FromBase64String(keyBase64);

            // Генерируем случайный IV
            using (var rng = RandomNumberGenerator.Create())
            {
                var ivBytes = new byte[IvSizeBytes];
                rng.GetBytes(ivBytes);

                // Создаём Aes объект (реализация AES-256)
                using (var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    // Шифруем данные
                    using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    {
                        // Преобразуем строку в байты
                        var plainBytes = Encoding.UTF8.GetBytes(plainText);

                        // Шифруем
                        var encryptedBytes = encryptor.TransformFinalBlock(
                            plainBytes, 0, plainBytes.Length);

                        // Создаём результат: IV + зашифрованные данные
                        var result = new byte[ivBytes.Length + encryptedBytes.Length];
                        Buffer.BlockCopy(ivBytes, 0, result, 0, ivBytes.Length);
                        Buffer.BlockCopy(encryptedBytes, 0, result, ivBytes.Length, encryptedBytes.Length);

                        // Преобразуем в Base64 для удобства хранения
                        var resultBase64 = Convert.ToBase64String(result);

                        _logger.LogDebug("Данные зашифрованы ({Length} символов)", plainText.Length);
                        return resultBase64;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при шифровании");
            throw;
        }
    }

    /// <summary>
    /// Дешифрует строку данных
    /// 
    /// Как это работает:
    /// 1. Извлекаем IV из начала (первые 16 байт)
    /// 2. Оставшаяся часть - это зашифрованные данные
    /// 3. Дешифруем с помощью главного ключа и извлечённого IV
    /// 
    /// Пример:
    ///   Входные данные: "base64_iv_here...base64_encrypted_here..."
    ///   Выходные данные: "Иван Петров"
    /// </summary>
    public async Task<string> DecryptAsync(string encryptedBase64)
    {
        try
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                return encryptedBase64;

            // Получаем главный ключ
            var keyBase64 = await SecureStorage.GetAsync(MasterKeyId);
            if (keyBase64 == null)
            {
                _logger.LogError("Главный ключ не найден!");
                throw new InvalidOperationException("Криптография не инициализирована");
            }

            var keyBytes = Convert.FromBase64String(keyBase64);

            // Преобразуем Base64 обратно в байты
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);

            // Извлекаем IV (первые 16 байт)
            var ivBytes = new byte[IvSizeBytes];
            Buffer.BlockCopy(encryptedBytes, 0, ivBytes, 0, IvSizeBytes);

            // Оставшаяся часть - это зашифрованные данные
            var cipherBytes = new byte[encryptedBytes.Length - IvSizeBytes];
            Buffer.BlockCopy(encryptedBytes, IvSizeBytes, cipherBytes, 0, cipherBytes.Length);

            // Создаём Aes объект
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Дешифруем данные
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    var decryptedBytes = decryptor.TransformFinalBlock(
                        cipherBytes, 0, cipherBytes.Length);

                    // Преобразуем байты обратно в строку UTF-8
                    var plainText = Encoding.UTF8.GetString(decryptedBytes);

                    _logger.LogDebug("Данные дешифрованы ({Length} символов)", plainText.Length);
                    return plainText;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при дешифровании");
            throw;
        }
    }

    /// <summary>
    /// Генерирует случайную строку (используется для кодов активации)
    /// 
    /// Пример: GenerateRandomCode(4, 4) → "5487-KE"
    /// </summary>
    public string GenerateRandomCode(int numericPart, int alphabeticPart)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var result = new StringBuilder();

            // Числовая часть
            if (numericPart > 0)
            {
                var numericBytes = new byte[numericPart];
                rng.GetBytes(numericBytes);
                for (int i = 0; i < numericPart; i++)
                {
                    result.Append(numericBytes[i] % 10);
                }
                result.Append("-");
            }

            // Буквенная часть (только прописные буквы, без похожих: 0, O, 1, I, L)
            const string allowedChars = "ABCDEFGHJKMNPQRSTUVWXYZ"; // Исключаем I, O, L для читаемости
            if (alphabeticPart > 0)
            {
                var alphaBytes = new byte[alphabeticPart];
                rng.GetBytes(alphaBytes);
                for (int i = 0; i < alphabeticPart; i++)
                {
                    result.Append(allowedChars[alphaBytes[i] % allowedChars.Length]);
                }
            }

            return result.ToString();
        }
    }

    /// <summary>
    /// Генерирует случайную соль для хеширования пароля
    /// Соль - это случайная строка, добавляется к паролю перед хешированием
    /// Это защищает от атак "радужные таблицы" (pre-computed hashes)
    /// </summary>
    public string GenerateSalt()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var saltBytes = new byte[SaltSizeBytes];
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }
    }
}

/// <summary>
/// Интерфейс для сервиса криптографии
/// Определяет контракт для всех криптографических операций
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Инициализирует криптографический сервис
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Шифрует строку данных с помощью AES-256
    /// </summary>
    Task<string> EncryptAsync(string plainText);

    /// <summary>
    /// Дешифрует строку данных с помощью AES-256
    /// </summary>
    Task<string> DecryptAsync(string encryptedBase64);

    /// <summary>
    /// Генерирует случайный код (для активации Telegram например)
    /// </summary>
    string GenerateRandomCode(int numericPart, int alphabeticPart);

    /// <summary>
    /// Генерирует соль для хеширования пароля
    /// </summary>
    string GenerateSalt();
}
