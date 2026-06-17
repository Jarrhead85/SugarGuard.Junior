// Класс для валидации различных данных
using System.Text.RegularExpressions;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Tests.Utilities;

public static class Validators
{
    /// <summary>
    /// Проверяет корректность email
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            return Regex.IsMatch(email, Constants.EmailRegex);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверяет корректность значения глюкозы
    /// Использует GlucoseLevels.IsValidInput()
    /// </summary>
    public static bool IsValidGlucoseValue(double glucose)
    {
        return GlucoseLevels.IsValidInput(glucose);
    }

    /// <summary>
    /// Проверяет корректность возраста
    /// Использует ChildProfileLimits.IsValidAge()
    /// </summary>
    public static bool IsValidAge(int age)
    {
        return ChildProfileLimits.IsValidAge(age);
    }

    /// <summary>
    /// Проверяет корректность веса
    /// Использует ChildProfileLimits.IsValidWeight()
    /// </summary>
    public static bool IsValidWeight(double weight)
    {
        return ChildProfileLimits.IsValidWeight(weight);
    }

    /// <summary>
    /// Проверяет корректность роста
    /// Использует ChildProfileLimits.IsValidHeight()
    /// </summary>
    public static bool IsValidHeight(double height)
    {
        return ChildProfileLimits.IsValidHeight(height);
    }
}