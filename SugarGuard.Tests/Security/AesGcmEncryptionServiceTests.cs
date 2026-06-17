using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.Junior.Core.Security;

namespace SugarGuard.Tests.Security;

/// <summary>
/// Тесты для <see cref="AesGcmEncryptionService"/>.
/// <para>
/// Покрывает:
/// <list type="bullet">
///   <item><description>Round-trip (encrypt → decrypt возвращает исходный plaintext)</description></item>
///   <item><description>Уникальность nonce (два encrypt одного plaintext → разный ciphertext)</description></item>
///   <item><description>Tag verification (подделка → CryptographicException)</description></item>
///   <item><description>Корректная работа с unicode/длинными строками</description></item>
///   <item><description>Граничные случаи (null, empty, whitespace)</description></item>
/// </list>
/// </para>
/// <para>
/// Решает: C-1 (release 1.0.0) — закрепляет, что production использует AES-GCM,
/// а не устаревший CBC.
/// </para>
/// </summary>
public class AesGcmEncryptionServiceTests
{
    private static AesGcmEncryptionService CreateSut(out InMemoryPlatformKeyProvider keyProvider)
    {
        keyProvider = new InMemoryPlatformKeyProvider();
        return new AesGcmEncryptionService(keyProvider, NullLogger<AesGcmEncryptionService>.Instance);
    }

    [Fact]
    public void CurrentVersion_IsAesGcm()
    {
        var sut = CreateSut(out _);
        Assert.Equal(EncryptionVersion.AesGcm, sut.CurrentVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("hello world")]
    [InlineData("Иван Петров")]
    [InlineData("Привет, мир! 你好, こんにちは")]
    [InlineData("0123456789")]
    public void RoundTrip_PlainText_ReturnsSameValue(string plain)
    {
        var sut = CreateSut(out _);

        var encrypted = sut.Encrypt(plain);
        var decrypted = sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void RoundTrip_LongString_10000Chars_Works()
    {
        var sut = CreateSut(out _);
        var plain = new string('A', 10_000);

        var encrypted = sut.Encrypt(plain);
        var decrypted = sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Encrypt_TwoCalls_ProduceDifferentCiphertexts_NonceUniqueness()
    {
        // GCM требует уникальный nonce для каждого encrypt с тем же ключом.
        // Иначе — катастрофическая потеря confidentiality.
        var sut = CreateSut(out _);
        var plain = "identical input";

        var enc1 = sut.Encrypt(plain);
        var enc2 = sut.Encrypt(plain);

        Assert.NotEqual(enc1.Ciphertext, enc2.Ciphertext);
        Assert.Equal(enc1.Version, enc2.Version);

        // Оба всё равно расшифровываются в одно значение
        Assert.Equal(plain, sut.Decrypt(enc1.Ciphertext, enc1.Version));
        Assert.Equal(plain, sut.Decrypt(enc2.Ciphertext, enc2.Version));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        // GCM tag ВАЛИДИРУЕТ целостность. Подделка должна быть обнаружена.
        // В .NET 8+ конкретный тип — AuthenticationTagMismatchException (наследник CryptographicException).
        var sut = CreateSut(out _);
        var encrypted = sut.Encrypt("secret data");
        var bytes = Convert.FromBase64String(encrypted.Ciphertext);

        // Подделываем 1 байт в середине ciphertext (после nonce, до tag)
        var tamperedPos = 12 + (bytes.Length - 12 - 16) / 2;
        bytes[tamperedPos] ^= 0xFF;

        var tamperedBase64 = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() =>
            sut.Decrypt(tamperedBase64, encrypted.Version));
    }

    [Fact]
    public void Decrypt_TamperedTag_ThrowsCryptographicException()
    {
        var sut = CreateSut(out _);
        var encrypted = sut.Encrypt("secret");
        var bytes = Convert.FromBase64String(encrypted.Ciphertext);

        // Подделываем последний байт (часть tag)
        bytes[bytes.Length - 1] ^= 0x01;

        var tamperedBase64 = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() =>
            sut.Decrypt(tamperedBase64, encrypted.Version));
    }

    [Fact]
    public void Decrypt_TruncatedCiphertext_ThrowsCryptographicException()
    {
        var sut = CreateSut(out _);
        var encrypted = sut.Encrypt("data");
        var bytes = Convert.FromBase64String(encrypted.Ciphertext);

        // Обрезаем наполовину
        var truncated = new byte[bytes.Length / 2];
        Array.Copy(bytes, truncated, truncated.Length);

        var truncatedBase64 = Convert.ToBase64String(truncated);

        Assert.ThrowsAny<CryptographicException>(() =>
            sut.Decrypt(truncatedBase64, encrypted.Version));
    }

    [Fact]
    public void Decrypt_NullOrEmpty_ReturnsAsIs()
    {
        var sut = CreateSut(out _);

        Assert.Equal("", sut.Decrypt("", EncryptionVersion.AesGcm));
        Assert.Null(sut.Decrypt(null!, EncryptionVersion.AesGcm));
    }

    [Fact]
    public void Decrypt_WrongVersion_ThrowsArgumentException()
    {
        var sut = CreateSut(out _);
        var encrypted = sut.Encrypt("data");

        // AesGcmEncryptionService не принимает версию LegacyCbc
        Assert.Throws<ArgumentException>(() =>
            sut.Decrypt(encrypted.Ciphertext, EncryptionVersion.LegacyCbc));
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        var sut = CreateSut(out _);

        var result = sut.Encrypt("");

        // Empty → empty (нет смысла шифровать)
        Assert.Equal("", result.Ciphertext);
    }
}

/// <summary>
/// Тесты для <see cref="VersionedEncryptionService"/>.
/// </summary>
public class VersionedEncryptionServiceTests
{
    private static (VersionedEncryptionService Sut, InMemoryPlatformKeyProvider KeyProvider) CreateSut()
    {
        var keyProvider = new InMemoryPlatformKeyProvider();
        var gcm = new AesGcmEncryptionService(keyProvider, NullLogger<AesGcmEncryptionService>.Instance);
        var legacy = new LegacyAesCbcDecryptionService(keyProvider, NullLogger<LegacyAesCbcDecryptionService>.Instance);
        var sut = new VersionedEncryptionService(gcm, legacy, NullLogger<VersionedEncryptionService>.Instance);
        return (sut, keyProvider);
    }

    [Fact]
    public void Encrypt_AlwaysReturnsAesGcmVersion()
    {
        var (sut, _) = CreateSut();

        var result = sut.Encrypt("test");

        Assert.Equal(EncryptionVersion.AesGcm, result.Version);
    }

    [Fact]
    public void Decrypt_AesGcmVersion_DelegatesToGcmService()
    {
        var (sut, _) = CreateSut();
        var encrypted = sut.Encrypt("hello");

        var decrypted = sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal("hello", decrypted);
    }

    [Fact]
    public void Decrypt_LegacyVersion_DelegatesToLegacyService()
    {
        var (sut, keyProvider) = CreateSut();

        // Создаём legacy-ciphertext тем же ключом, что использует SUT.
        // Это симулирует запись из v1.0.0-beta (CBC), которую мы хотим прочитать.
        var key = keyProvider.GetOrCreateKey();
        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);
        var plain = "старые данные из v1.0.0";

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var cipherBytes = aes.CreateEncryptor().TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var combined = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, iv.Length, cipherBytes.Length);
        var legacyCiphertext = Convert.ToBase64String(combined);

        // Расшифровываем через VersionedEncryptionService
        var decrypted = sut.Decrypt(legacyCiphertext, EncryptionVersion.LegacyCbc);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Decrypt_UnknownVersion_ThrowsArgumentOutOfRange()
    {
        var (sut, _) = CreateSut();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sut.Decrypt("any", (EncryptionVersion)99));
    }
}
