using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// Реализация <see cref="IEncryptionService"/> для LEGACY AES-256-CBC.
/// <para>
/// <b>⚠️ ТОЛЬКО ДЛЯ ЧТЕНИЯ СУЩЕСТВУЮЩИХ ДАННЫХ.</b> AES-CBC без аутентификации
/// уязвим к padding oracle и bit-flipping атакам. Новые записи НЕ ДОЛЖНЫ
/// создаваться через эту реализацию — используйте <see cref="AesGcmEncryptionService"/>.
/// </para>
/// <para>
/// <b>Метод <see cref="Encrypt"/> помечен как <see cref="ObsoleteAttribute"/>.</b>
/// Если его вызвать — будет <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// <b>Формат legacy данных:</b> <c>iv(16) || ciphertext(N)</c>, всё в Base64.
/// </para>
/// </summary>
public sealed class LegacyAesCbcDecryptionService : IEncryptionService
{
    private const int IvSizeBytes = 16;

    private readonly IPlatformKeyProvider _keyProvider;
    private readonly ILogger<LegacyAesCbcDecryptionService> _logger;

    public EncryptionVersion CurrentVersion => EncryptionVersion.LegacyCbc;

    public LegacyAesCbcDecryptionService(
        IPlatformKeyProvider keyProvider,
        ILogger<LegacyAesCbcDecryptionService> logger)
    {
        _keyProvider = keyProvider;
        _logger = logger;
    }

    [Obsolete("Новые записи должны использовать AES-GCM (см. AesGcmEncryptionService). Этот метод существует только для тестов миграции.")]
    public EncryptedValue Encrypt(string plaintext)
    {
        throw new NotSupportedException(
            "Legacy CBC устарел. Новые записи должны шифроваться через AES-GCM. " +
            "Этот метод сохранён только для unit-тестов миграции данных.");
    }

    public string Decrypt(string ciphertext, EncryptionVersion version)
    {
        if (version != EncryptionVersion.LegacyCbc)
        {
            throw new ArgumentException(
                $"LegacyAesCbcDecryptionService не может расшифровать версию {version}.",
                nameof(version));
        }

        if (ciphertext is null)
        {
            return null!;
        }
        if (ciphertext.Length == 0)
        {
            return string.Empty;
        }

        var key = _keyProvider.GetOrCreateKey();
        var encryptedBytes = Convert.FromBase64String(ciphertext);

        if (encryptedBytes.Length < IvSizeBytes + 1)
        {
            throw new CryptographicException(
                $"Legacy CBC зашифрованное значение слишком короткое: {encryptedBytes.Length} байт.");
        }

        var iv = new byte[IvSizeBytes];
        var cipherBytes = new byte[encryptedBytes.Length - IvSizeBytes];
        Buffer.BlockCopy(encryptedBytes, 0, iv, 0, IvSizeBytes);
        Buffer.BlockCopy(encryptedBytes, IvSizeBytes, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plaintext = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        _logger.LogDebug("CBC-decrypted {Len} bytes", plaintext.Length);
        return Encoding.UTF8.GetString(plaintext);
    }
}
