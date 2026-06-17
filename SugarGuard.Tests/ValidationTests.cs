using SugarGuard.Tests.Utilities;

namespace SugarGuard.Tests;

/// <summary>
/// Тесты для валидации данных
/// Проверяем корректность валидаторов без зависимости от MAUI
/// </summary>
public class ValidationTests
{
    /// <summary>
    /// Тест валидации глюкозы - корректные значения
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(5.5)]
    [InlineData(15.0)]
    [InlineData(30.0)]
    public void IsValidGlucoseValue_ValidValues_ShouldReturnTrue(double glucose)
    {
        // ACT
        var result = Validators.IsValidGlucoseValue(glucose);

        // ASSERT
        Assert.True(result, $"Значение глюкозы {glucose} должно быть валидным");
    }

    /// <summary>
    /// Тест валидации глюкозы - некорректные значения
    /// </summary>
    [Theory]
    [InlineData(0.9)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(30.1)]
    [InlineData(50.0)]
    public void IsValidGlucoseValue_InvalidValues_ShouldReturnFalse(double glucose)
    {
        // ACT
        var result = Validators.IsValidGlucoseValue(glucose);

        // ASSERT
        Assert.False(result, $"Значение глюкозы {glucose} должно быть невалидным");
    }

    /// <summary>
    /// Тест валидации возраста - корректные значения
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(18)]
    public void IsValidAge_ValidValues_ShouldReturnTrue(int age)
    {
        // ACT
        var result = Validators.IsValidAge(age);

        // ASSERT
        Assert.True(result, $"Возраст {age} должен быть валидным");
    }

    /// <summary>
    /// Тест валидации возраста - некорректные значения
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(19)]
    [InlineData(25)]
    public void IsValidAge_InvalidValues_ShouldReturnFalse(int age)
    {
        // ACT
        var result = Validators.IsValidAge(age);

        // ASSERT
        Assert.False(result, $"Возраст {age} должен быть невалидным");
    }

    /// <summary>
    /// Тест валидации веса - корректные значения
    /// </summary>
    [Theory]
    [InlineData(10.0)]
    [InlineData(50.5)]
    [InlineData(200.0)]
    public void IsValidWeight_ValidValues_ShouldReturnTrue(double weight)
    {
        // ACT
        var result = Validators.IsValidWeight(weight);

        // ASSERT
        Assert.True(result, $"Вес {weight} должен быть валидным");
    }

    /// <summary>
    /// Тест валидации веса - некорректные значения
    /// </summary>
    [Theory]
    [InlineData(9.9)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(200.1)]
    [InlineData(300.0)]
    public void IsValidWeight_InvalidValues_ShouldReturnFalse(double weight)
    {
        // ACT
        var result = Validators.IsValidWeight(weight);

        // ASSERT
        Assert.False(result, $"Вес {weight} должен быть невалидным");
    }

    /// <summary>
    /// Тест валидации роста - корректные значения
    /// </summary>
    [Theory]
    [InlineData(60.0)]
    [InlineData(150.0)]
    [InlineData(200.0)]
    public void IsValidHeight_ValidValues_ShouldReturnTrue(double height)
    {
        // ACT
        var result = Validators.IsValidHeight(height);

        // ASSERT
        Assert.True(result, $"Рост {height} должен быть валидным");
    }

    /// <summary>
    /// Тест валидации роста - некорректные значения
    /// </summary>
    [Theory]
    [InlineData(59.9)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(200.1)]
    [InlineData(250.0)]
    public void IsValidHeight_InvalidValues_ShouldReturnFalse(double height)
    {
        // ACT
        var result = Validators.IsValidHeight(height);

        // ASSERT
        Assert.False(result, $"Рост {height} должен быть невалидным");
    }

    /// <summary>
    /// Тест валидации email - корректные значения
    /// </summary>
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.org")]
    [InlineData("admin@test.ru")]
    public void IsValidEmail_ValidEmails_ShouldReturnTrue(string email)
    {
        // ACT
        var result = Validators.IsValidEmail(email);

        // ASSERT
        Assert.True(result, $"Email {email} должен быть валидным");
    }

    /// <summary>
    /// Тест валидации email - некорректные значения
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("@domain.com")]
    [InlineData("user@")]
    [InlineData("user@domain")]
    public void IsValidEmail_InvalidEmails_ShouldReturnFalse(string email)
    {
        // ACT
        var result = Validators.IsValidEmail(email);

        // ASSERT
        Assert.False(result, $"Email {email} должен быть невалидным");
    }


}