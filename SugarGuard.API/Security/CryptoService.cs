using System.Security.Cryptography;
using System.Text;

namespace SugarGuard.API.Security;

/// <summary>
/// Серверный криптосервис для шифрования PHI-данных
/// </summary>
public sealed class CryptoService : ICryptoService
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;
    private readonly ILogger<CryptoService> _logger;

    /// <summary>
    /// Инициализирует сервис ключом из конфигурации
    /// </summary>
    public CryptoService(IConfiguration configuration, ILogger<CryptoService> logger)
    {
        _logger = logger;

        var keyBase64 = Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY")
            ?? configuration["Crypto:PhiEncryptionKey"];

        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException(
                "PHI_ENCRYPTION_KEY не задан. Задайте переменную окружения или Crypto:PhiEncryptionKey.");

        var keyBytes = Convert.FromBase64String(keyBase64);

        if (keyBytes.Length != KeySizeBytes)
            throw new InvalidOperationException(
                $"PHI_ENCRYPTION_KEY должен быть {KeySizeBytes} байт (256 бит). Получено: {keyBytes.Length} байт.");

        _key = keyBytes;
    }

    /// <inheritdoc/>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            var nonce = new byte[NonceSizeBytes];
            RandomNumberGenerator.Fill(nonce);

            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSizeBytes];

            using var aesGcm = new AesGcm(_key, TagSizeBytes);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            var result = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
            Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
            Buffer.BlockCopy(cipherBytes, 0, result, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка шифрования PHI-данных.");
            throw;
        }
    }

    /// <inheritdoc/>
    public string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return encryptedBase64;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);

            if (encryptedBytes.Length < NonceSizeBytes + TagSizeBytes)
                throw new CryptographicException("Недостаточно данных для расшифровки: повреждён шифртекст.");

            var nonce = new byte[NonceSizeBytes];
            var tag = new byte[TagSizeBytes];
            var cipherBytes = new byte[encryptedBytes.Length - NonceSizeBytes - TagSizeBytes];

            Buffer.BlockCopy(encryptedBytes, 0, nonce, 0, NonceSizeBytes);
            Buffer.BlockCopy(encryptedBytes, NonceSizeBytes, tag, 0, TagSizeBytes);
            Buffer.BlockCopy(encryptedBytes, NonceSizeBytes + TagSizeBytes, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];

            using var aesGcm = new AesGcm(_key, TagSizeBytes);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            _logger.LogError(ex, "Ошибка проверки целостности при расшифровке PHI. Возможна подмена данных.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка расшифровки PHI-данных.");
            throw;
        }
    }
}
