using SugarGuard.Junior.Utilities;

namespace SugarGuard.Tests.Security;

/// <summary>
/// Unit-тесты для <see cref="CipherFormat"/>.
/// Покрывает определение версии шифрования по префиксу в ciphertext.
/// </summary>
public class CipherFormatTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plaintext")]
    public void IsEncrypted_EmptyOrPlaintext_ReturnsFalse(string? value)
    {
        Assert.False(CipherFormat.IsEncrypted(value));
    }

    [Theory]
    [InlineData("1:abc")]
    [InlineData("2:abc")]
    public void IsEncrypted_WithVersionPrefix_ReturnsTrue(string value)
    {
        Assert.True(CipherFormat.IsEncrypted(value));
    }

    [Fact]
    public void IsEncrypted_LegacyBase64_StillDetected()
    {
        // Legacy ciphertext: base64(IV+ciphertext), кончается на '=' padding.
        var legacy = Convert.ToBase64String(new byte[32]) + "==";
        Assert.True(CipherFormat.IsEncrypted(legacy));
    }

    [Fact]
    public void IsEncrypted_TooShortBase64_NotDetected()
    {
        // "< 16 chars" — heuristic не срабатывает на коротком тексте с '='.
        Assert.False(CipherFormat.IsEncrypted("a="));
    }

    [Theory]
    [InlineData("1:abc", true)]
    [InlineData("2:abc", true)]
    [InlineData("3:abc", false)]   // неизвестная версия
    [InlineData("abc", false)]     // нет префикса
    [InlineData(null, false)]
    [InlineData("", false)]
    public void HasVersionPrefix_DetectsOnlyKnownVersions(string? value, bool expected)
    {
        Assert.Equal(expected, CipherFormat.HasVersionPrefix(value));
    }

    [Fact]
    public void HasVersionPrefix_RejectsShortInput()
    {
        Assert.False(CipherFormat.HasVersionPrefix("1"));
        Assert.False(CipherFormat.HasVersionPrefix(":"));
        Assert.False(CipherFormat.HasVersionPrefix("1:"));
    }
}
