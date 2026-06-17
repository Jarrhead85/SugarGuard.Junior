using System.Security.Cryptography;

namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// In-memory реализация <see cref="IPlatformKeyProvider"/> для unit-тестов
/// и dev-режима. В production НЕ использовать — ключ теряется при перезапуске.
/// <para>
/// В production: использовать <c>AndroidKeyStoreKeyProvider</c> на Android
/// (через <c>KeyGenParameterSpec</c> с <c>setKeySize(256)</c> и
/// <c>setRandomizedEncryptionRequired(true)</c>), и <c>KeychainKeyProvider</c>
/// на iOS/macOS (через <c>SecKeyChain</c> с <c>kSecAttrAccessibleWhenUnlockedThisDeviceOnly</c>).
/// </para>
/// </summary>
public sealed class InMemoryPlatformKeyProvider : IPlatformKeyProvider
{
    private readonly object _lock = new();
    private byte[]? _key;

    public byte[] GetOrCreateKey()
    {
        if (_key is not null) return _key;

        lock (_lock)
        {
            if (_key is not null) return _key;

            var newKey = new byte[32]; // AES-256
            RandomNumberGenerator.Fill(newKey);
            _key = newKey;
            return _key;
        }
    }
}
