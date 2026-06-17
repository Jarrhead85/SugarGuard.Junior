using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// Реализация <see cref="IEncryptionService"/> на базе AES-256-GCM.
/// <para>
/// <b>Безопасность:</b> AES-GCM — аутентифицированное шифрование, защищает
/// не только конфиденциальность, но и целостность данных (tampering detection).
/// Каждый ciphertext имеет уникальный 12-байтный nonce, и в конце — 16-байтный
/// authentication tag. Подделка tag → <see cref="CryptographicException"/> при decrypt.
/// </para>
/// <para>
/// <b>Хранение ключа:</b> в этой реализации ключ предоставляется через
/// <see cref="IPlatformKeyProvider"/>. В production на Android используется
/// AndroidKeyStore (см. <c>AndroidKeyStoreKeyProvider</c> — TODO), в тестах —
/// <see cref="InMemoryPlatformKeyProvider"/>.
/// </para>
/// <para>
/// <b>Формат на диске:</b> <c>nonce(12) || ciphertext(N) || tag(16)</c>, всё в Base64.
/// </para>
/// </summary>
public sealed class AesGcmEncryptionService : IEncryptionService
{
    // Размеры согласно RFC 5116 (AES-GCM)
    private const int NonceSizeBytes = 12;   // 96 бит — рекомендованный для GCM
    private const int TagSizeBytes = 16;     // 128 бит

    private readonly IPlatformKeyProvider _keyProvider;
    private readonly ILogger<AesGcmEncryptionService> _logger;

    public EncryptionVersion CurrentVersion => EncryptionVersion.AesGcm;

    public AesGcmEncryptionService(
        IPlatformKeyProvider keyProvider,
        ILogger<AesGcmEncryptionService> logger)
    {
        _keyProvider = keyProvider;
        _logger = logger;
    }

    public EncryptedValue Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return new EncryptedValue(plaintext ?? string.Empty, CurrentVersion);
        }

        var key = _keyProvider.GetOrCreateKey();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSizeBytes];
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        // Генерируем уникальный nonce для каждой записи.
        // RandomNumberGenerator гарантирует криптостойкую случайность.
        RandomNumberGenerator.Fill(nonce);

        // AES-GCM encrypt: nonce должен быть уникальным, key 32 байта (AES-256)
        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Склеиваем: nonce || ciphertext || tag
        var result = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes + ciphertext.Length, TagSizeBytes);

        var base64 = Convert.ToBase64String(result);
        _logger.LogDebug("GCM-encrypted {Len} bytes → {CipLen} bytes", plaintextBytes.Length, ciphertext.Length);

        return new EncryptedValue(base64, CurrentVersion);
    }

    public string Decrypt(string ciphertext, EncryptionVersion version)
    {
        if (version != EncryptionVersion.AesGcm)
        {
            throw new ArgumentException(
                $"AesGcmEncryptionService не может расшифровать версию {version}. Используйте VersionedEncryptionService.",
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
        var combined = Convert.FromBase64String(ciphertext);

        if (combined.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException(
                $"Зашифрованное значение слишком короткое: {combined.Length} байт (мин. {NonceSizeBytes + TagSizeBytes}).");
        }

        var nonce = new byte[NonceSizeBytes];
        var tag = new byte[TagSizeBytes];
        var cipherBytes = new byte[combined.Length - NonceSizeBytes - TagSizeBytes];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(combined, NonceSizeBytes, cipherBytes, 0, cipherBytes.Length);
        Buffer.BlockCopy(combined, NonceSizeBytes + cipherBytes.Length, tag, 0, TagSizeBytes);

        var plaintext = new byte[cipherBytes.Length];
        using var aesGcm = new AesGcm(key, TagSizeBytes);
        // Decrypt ВАЛИДИРУЕТ tag. Если tag не совпадает → CryptographicException.
        aesGcm.Decrypt(nonce, cipherBytes, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
