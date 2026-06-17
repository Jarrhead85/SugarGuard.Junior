using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Security;

namespace SugarGuard.Junior.Security;

/// <summary>
/// Реализация существующего <see cref="ICryptoService"/> поверх
/// <see cref="IEncryptionService"/> (AES-256-GCM) из
/// <c>SugarGuard.Junior.Core</c>.
/// <para>
/// заменяет <c>CryptoService</c> (AES-256-CBC) на
/// versioned encryption с префиксом версии в ciphertext. Это позволяет:
/// <list type="number">
///   <item><description>Читать legacy CBC-данные (миграция без даунтайма).</description></item>
///   <item><description>Писать новые данные в GCM (аутентифицированное шифрование).</description></item>
///   <item><description>Запустить фоновый re-encrypt через <see cref="MauiReEncryptJob"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Формат ciphertext:</b> <c>{version}:{base64}</c>, где <c>version</c> =
/// <c>1</c> для legacy CBC и <c>2</c> для GCM. Префикс однозначно идентифицирует
/// версию, не требует миграций БД и работает с любыми pre-existing данными.
/// </para>
/// </summary>
public sealed class MauiEncryptionService : ICryptoService
{
    private const string LegacyCbcPrefix = "1:";
    private const string AesGcmPrefix = "2:";

    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<MauiEncryptionService> _logger;

    public MauiEncryptionService(
        IEncryptionService encryptionService,
        ILogger<MauiEncryptionService> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Инициализация не требуется — мастер-ключ управляется
    /// <see cref="MauiSecureStorageKeyProvider"/> через DI singleton.
    /// Метод сохранён для совместимости с <see cref="ICryptoService"/>.
    /// </remarks>
    public Task<bool> InitializeAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task<string> EncryptAsync(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return Task.FromResult(plainText);

        // Пишем всегда в GCM (текущая версия).
        var encrypted = _encryptionService.Encrypt(plainText);
        return Task.FromResult(AesGcmPrefix + encrypted.Ciphertext);
    }

    /// <inheritdoc />
    public Task<string> DecryptAsync(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return Task.FromResult(encryptedBase64);

        // Уже-расшифрованный plaintext (idempotency для legacy-кода).
        if (!HasVersionPrefix(encryptedBase64))
        {
            return Task.FromResult(encryptedBase64);
        }

        try
        {
            if (encryptedBase64.StartsWith(LegacyCbcPrefix, StringComparison.Ordinal))
            {
                var legacyCiphertext = encryptedBase64[LegacyCbcPrefix.Length..];
                var plain = _encryptionService.Decrypt(legacyCiphertext, EncryptionVersion.LegacyCbc);
                return Task.FromResult(plain);
            }

            if (encryptedBase64.StartsWith(AesGcmPrefix, StringComparison.Ordinal))
            {
                var gcmCiphertext = encryptedBase64[AesGcmPrefix.Length..];
                var plain = _encryptionService.Decrypt(gcmCiphertext, EncryptionVersion.AesGcm);
                return Task.FromResult(plain);
            }

            throw new InvalidOperationException(
                $"Unknown encryption version prefix in ciphertext (length={encryptedBase64.Length}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed for ciphertext (prefix={Prefix}).",
                GetPrefix(encryptedBase64));
            throw;
        }
    }

    /// <inheritdoc />
    public string GenerateRandomCode(int numericPart, int alphabeticPart)
    {
        return LegacyRandomCode.Generate(numericPart, alphabeticPart);
    }

    /// <inheritdoc />
    public string GenerateSalt()
    {
        return LegacyRandomCode.GenerateSalt();
    }

    private static bool HasVersionPrefix(string s) =>
        s.Length >= 2 && s[1] == ':' && (s[0] == '1' || s[0] == '2');

    private static string GetPrefix(string s) =>
        s.Length >= 2 && s[1] == ':' ? s[..2] : "<none>";
}

/// <summary>
/// Утилита для генерации кодов и солей — изолирована, чтобы
/// <see cref="MauiEncryptionService"/> не зависел от legacy-CryptoService.
/// </summary>
internal static class LegacyRandomCode
{
    public static string Generate(int numericPart, int alphabeticPart)
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var result = new System.Text.StringBuilder();

        if (numericPart > 0)
        {
            var numericBytes = new byte[numericPart];
            rng.GetBytes(numericBytes);
            for (int i = 0; i < numericPart; i++)
            {
                result.Append(numericBytes[i] % 10);
            }
            result.Append('-');
        }

        const string allowedChars = "ABCDEFGHJKMNPQRSTUVWXYZ";
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

    public static string GenerateSalt()
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var saltBytes = new byte[16];
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }
}

