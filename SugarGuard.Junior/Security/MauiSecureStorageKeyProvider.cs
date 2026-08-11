using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Security;

namespace SugarGuard.Junior.Security;

/// <summary>
/// Платформо-зависимый поставщик мастер-ключа AES-256 для MAUI.
/// <para>
/// Использует <see cref="SecureStorage"/>, который под капотом:
/// <list type="bullet">
///   <item><description>Android: <c>AndroidKeyStore</c> + <c>EncryptedSharedPreferences</c></description></item>
///   <item><description>iOS/macOS: <c>SecKeyChain</c> с <c>kSecAttrAccessibleWhenUnlockedThisDeviceOnly</c></description></item>
///   <item><description>Windows: <c>Data Protection API</c> (DPAPI) per-user</description></item>
/// </list>
/// </para>
/// <para>
/// заменяет <c>CryptoService.InitializeAsync()</c>, который
/// хранил ключ в <c>SecureStorage</c> как base64-string. Теперь ключ по-прежнему
/// хранится там же, но получается синхронно через интерфейс
/// <see cref="IPlatformKeyProvider"/>, что позволяет <see cref="AesGcmEncryptionService"/>
/// использовать его без I/O на каждую операцию.
/// </para>
/// </summary>
public sealed class MauiSecureStorageKeyProvider : IPlatformKeyProvider
{
    private const string KeyStorageKey = "sugarguard_master_key_v2";
    private const string LegacyKeyStorageKey = "sugarguard_master_key";
    private const int Aes256KeySize = 32;

    private readonly ILogger<MauiSecureStorageKeyProvider> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private byte[]? _cachedKey;

    public MauiSecureStorageKeyProvider(ILogger<MauiSecureStorageKeyProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public byte[] GetOrCreateKey()
    {
        if (_cachedKey is not null)
            return _cachedKey;

        throw new InvalidOperationException(
            "Ключ шифрования ещё не подготовлен. Вызовите InitializeAsync при запуске приложения.");
    }

    /// <summary>
    /// Асинхронно получает или создаёт мастер-ключ до обращения к локальным
    /// зашифрованным данным. Не блокирует UI-поток синхронным ожиданием Task.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedKey is not null)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedKey is not null)
            {
                return;
            }

            var stored = await SecureStorage.GetAsync(KeyStorageKey);

            // Preserve access to data created by the legacy CBC service. The
            // same 256-bit key is valid for the one-time CBC -> GCM migration;
            // generating a different v2 key here would make existing rows
            // permanently unreadable.
            if (string.IsNullOrEmpty(stored))
            {
                var legacyStored = await SecureStorage.GetAsync(LegacyKeyStorageKey);
                if (TryDecodeAes256Key(legacyStored, out var legacyKey))
                {
                    await SecureStorage.SetAsync(KeyStorageKey, legacyStored!);
                    _cachedKey = legacyKey;
                    _logger.LogInformation("Legacy master key migrated to the versioned key slot.");
                    return;
                }

                if (!string.IsNullOrEmpty(legacyStored))
                {
                    throw new CryptographicException(
                        "The legacy encryption key is invalid. Refusing to replace it because that would destroy access to local data.");
                }
            }

            if (TryDecodeAes256Key(stored, out var storedKey))
            {
                _cachedKey = storedKey;
                _logger.LogDebug("Master key loaded from SecureStorage ({Len} bytes).", _cachedKey.Length);
                return;
            }

            if (!string.IsNullOrEmpty(stored))
            {
                throw new CryptographicException(
                    "The stored encryption key is invalid. Refusing to replace it because that would destroy access to local data.");
            }

            // Генерируем новый ключ AES-256.
            var newKey = new byte[Aes256KeySize];
            RandomNumberGenerator.Fill(newKey);

            await SecureStorage.SetAsync(KeyStorageKey, Convert.ToBase64String(newKey));

            _cachedKey = newKey;
            _logger.LogInformation("New AES-256 master key generated and stored in SecureStorage.");
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static bool TryDecodeAes256Key(string? encoded, out byte[] key)
    {
        key = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(encoded);
            return key.Length == Aes256KeySize;
        }
        catch (FormatException)
        {
            key = Array.Empty<byte>();
            return false;
        }
    }
}

