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
    private const int Aes256KeySize = 32;

    private readonly ILogger<MauiSecureStorageKeyProvider> _logger;
    private readonly object _lock = new();
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

        lock (_lock)
        {
            if (_cachedKey is not null)
                return _cachedKey;

            // SecureStorage на MAUI синхронный API не предоставляет, но на практике
            // на Android/iOS/Windows он не делает I/O на горячем пути после первого
            // чтения. Для безопасности используем Task.Run.
            var stored = Task.Run(async () => await SecureStorage.GetAsync(KeyStorageKey)).GetAwaiter().GetResult();

            if (!string.IsNullOrEmpty(stored))
            {
                try
                {
                    _cachedKey = Convert.FromBase64String(stored);
                    if (_cachedKey.Length == Aes256KeySize)
                    {
                        _logger.LogDebug("Master key loaded from SecureStorage ({Len} bytes).", _cachedKey.Length);
                        return _cachedKey;
                    }
                    _logger.LogWarning("Stored key has wrong length {Len}, regenerating.", _cachedKey.Length);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Stored key is not valid Base64, regenerating.");
                }
            }

            // Генерируем новый ключ AES-256.
            var newKey = new byte[Aes256KeySize];
            RandomNumberGenerator.Fill(newKey);

            Task.Run(async () => await SecureStorage.SetAsync(KeyStorageKey, Convert.ToBase64String(newKey)))
                .GetAwaiter()
                .GetResult();

            _cachedKey = newKey;
            _logger.LogInformation("New AES-256 master key generated and stored in SecureStorage.");
            return _cachedKey;
        }
    }
}

