using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.Junior.Core.Security;

namespace SugarGuard.Tests.Security;

/// <summary>
/// Round-trip тесты для шифрования PHI данных на production-ready AES-256-GCM.
/// <para>
/// ВАЖНО: эти тесты больше НЕ проверяют AES-CBC (см. <c>Release 1.0.0 / C-1</c>).
/// CBC-уязвимости (padding oracle, bit-flipping) устранены переходом на GCM.
/// </para>
/// <para>
/// Покрывает PHI-данные приложения:
/// <list type="bullet">
///   <item><description>Глюкоза (значение + точка измерения)</description></item>
///   <item><description>Заметки на кириллице (PHI)</description></item>
///   <item><description>Названия перекусов (PHI)</description></item>
///   <item><description>Хлебные единицы, чувствительность к инсулину, carb ratio</description></item>
///   <item><description>Целевой диапазон глюкозы</description></item>
/// </list>
/// </para>
/// <para>
/// Validates: Requirements 5.1, 5.2, 5.3, 5.4 (Property 4: PHI encryption round-trip)
/// </para>
/// </summary>
public class EncryptionRoundTripTests
{
    private readonly AesGcmEncryptionService _sut;

    public EncryptionRoundTripTests()
    {
        var keyProvider = new InMemoryPlatformKeyProvider();
        _sut = new AesGcmEncryptionService(keyProvider, NullLogger<AesGcmEncryptionService>.Instance);
    }

    [Fact]
    public void EncryptDecrypt_SimpleString_ShouldReturnOriginal()
    {
        var original = "Test Data";

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.NotEqual(original, encrypted.Ciphertext);
        Assert.Equal(original, decrypted);
    }

    [Theory]
    [InlineData(3.5)]
    [InlineData(5.5)]
    [InlineData(10.2)]
    [InlineData(15.8)]
    public void EncryptDecrypt_GlucoseValue_ShouldReturnOriginal(double glucoseValue)
    {
        var original = glucoseValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);
        var decryptedValue = double.Parse(decrypted, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(glucoseValue, decryptedValue);
    }

    [Theory]
    [InlineData("BeforeMeal")]
    [InlineData("AfterMeal")]
    [InlineData("BeforeSleep")]
    [InlineData("AfterSleep")]
    [InlineData("BeforeExercise")]
    [InlineData("AfterExercise")]
    public void EncryptDecrypt_ChildState_ShouldReturnOriginal(string state)
    {
        var encrypted = _sut.Encrypt(state);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(state, decrypted);
    }

    [Theory]
    [InlineData("Чувствую себя хорошо")]
    [InlineData("Немного устал после тренировки")]
    [InlineData("Съел яблоко")]
    public void EncryptDecrypt_CyrillicNotes_ShouldReturnOriginal(string notes)
    {
        var encrypted = _sut.Encrypt(notes);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(notes, decrypted);
    }

    [Theory]
    [InlineData("Яблоко")]
    [InlineData("Банан")]
    [InlineData("Шоколадка")]
    [InlineData("Сок апельсиновый")]
    public void EncryptDecrypt_SnackName_ShouldReturnOriginal(string snackName)
    {
        var encrypted = _sut.Encrypt(snackName);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(snackName, decrypted);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(3.75)]
    [InlineData(5.0)]
    public void EncryptDecrypt_BreadUnits_ShouldReturnOriginal(double breadUnits)
    {
        var original = breadUnits.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);
        var decryptedValue = double.Parse(decrypted, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(breadUnits, decryptedValue, precision: 2);
    }

    [Theory]
    [InlineData(4.0, 10.0)]
    [InlineData(3.9, 9.5)]
    [InlineData(4.5, 11.0)]
    public void EncryptDecrypt_TargetRange_ShouldReturnOriginal(double min, double max)
    {
        var minText = min.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        var maxText = max.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        var encMin = _sut.Encrypt(minText);
        var encMax = _sut.Encrypt(maxText);
        var decMin = _sut.Decrypt(encMin.Ciphertext, encMin.Version);
        var decMax = _sut.Decrypt(encMax.Ciphertext, encMax.Version);

        Assert.Equal(min, double.Parse(decMin, System.Globalization.CultureInfo.InvariantCulture), precision: 1);
        Assert.Equal(max, double.Parse(decMax, System.Globalization.CultureInfo.InvariantCulture), precision: 1);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(2.5)]
    public void EncryptDecrypt_InsulinSensitivity_ShouldReturnOriginal(double sensitivity)
    {
        var original = sensitivity.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);
        var decryptedValue = double.Parse(decrypted, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(sensitivity, decryptedValue, precision: 2);
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(10.0)]
    [InlineData(12.0)]
    [InlineData(15.0)]
    public void EncryptDecrypt_CarbInsulinRatio_ShouldReturnOriginal(double ratio)
    {
        var original = ratio.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);
        var decryptedValue = double.Parse(decrypted, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(ratio, decryptedValue, precision: 2);
    }

    [Fact]
    public void EncryptDecrypt_EmptyString_ShouldReturnEmpty()
    {
        var encrypted = _sut.Encrypt("");
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal("", encrypted.Ciphertext);
        Assert.Equal("", decrypted);
    }

    [Fact]
    public void EncryptDecrypt_NullString_ShouldReturnEmpty()
    {
        var encrypted = _sut.Encrypt(null!);

        Assert.Equal("", encrypted.Ciphertext);
    }

    [Fact]
    public void Encrypt_SameData_ShouldProduceDifferentCiphertext_NonceUniqueness()
    {
        var original = "Test Data";

        var enc1 = _sut.Encrypt(original);
        var enc2 = _sut.Encrypt(original);

        Assert.NotEqual(enc1.Ciphertext, enc2.Ciphertext);
        Assert.Equal(original, _sut.Decrypt(enc1.Ciphertext, enc1.Version));
        Assert.Equal(original, _sut.Decrypt(enc2.Ciphertext, enc2.Version));
    }

    [Fact]
    public void EncryptDecrypt_LongString_ShouldReturnOriginal()
    {
        var original = "Это очень длинная строка с большим количеством текста, " +
                       "которая содержит различные символы: цифры 123456, " +
                       "специальные символы !@#$%^&*(), и кириллицу. " +
                       "Она должна корректно шифроваться и дешифроваться.";

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(original, decrypted);
    }

    [Theory]
    [InlineData("!@#$%^&*()")]
    [InlineData("Test\nWith\nNewlines")]
    [InlineData("Test\tWith\tTabs")]
    [InlineData("Test \"With\" Quotes")]
    public void EncryptDecrypt_SpecialCharacters_ShouldReturnOriginal(string text)
    {
        var encrypted = _sut.Encrypt(text);
        var decrypted = _sut.Decrypt(encrypted.Ciphertext, encrypted.Version);

        Assert.Equal(text, decrypted);
    }
}
