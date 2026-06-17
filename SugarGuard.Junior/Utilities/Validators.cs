// Класс для валидации различных данных
using System.Text.RegularExpressions;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Junior.Utilities;

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
    /// Проверяет корректность номера телефона
    /// Формат: +7 XXX XXX-XX-XX
    /// </summary>
    public static bool IsValidPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        try
        {
            return Regex.IsMatch(phone, Constants.PhoneRegex);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверяет мощность пароля
    /// Требования:
    /// - Минимум 8 символов
    /// - Хотя бы одна заглавная буква
    /// - Хотя бы одна цифра
    /// - Хотя бы один спецсимвол (<c>!@#$%^&amp;*</c>)
    /// </summary>
    public static (bool isValid, List<string> errors) IsValidPassword(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Пароль не может быть пустым");
            return (false, errors);
        }

        if (password.Length < Constants.PasswordMinLength)
            errors.Add($"Минимум {Constants.PasswordMinLength} символов");

        if (!Regex.IsMatch(password, "[A-Z]"))
            errors.Add("Требуется заглавная буква (A-Z)");

        if (!Regex.IsMatch(password, "[0-9]"))
            errors.Add("Требуется цифра (0-9)");

        if (!Regex.IsMatch(password, $"[{Regex.Escape(Constants.PasswordRequiresSpecial)}]"))
            errors.Add("Требуется спецсимвол (!@#$%^&*)");

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Проверяет корректность значения глюкозы
    /// </summary>
    public static bool IsValidGlucoseValue(double glucose)
    {
        return GlucoseLevels.IsValidInput(glucose);
    }

    /// <summary>
    /// Проверяет корректность возраста
    /// </summary>
    public static bool IsValidAge(int age)
    {
        return ChildProfileLimits.IsValidAge(age);
    }

    /// <summary>
    /// Проверяет корректность веса
    /// </summary>
    public static bool IsValidWeight(double weight)
    {
        return ChildProfileLimits.IsValidWeight(weight);
    }

    /// <summary>
    /// Проверяет корректность роста
    /// </summary>
    public static bool IsValidHeight(double height)
    {
        return ChildProfileLimits.IsValidHeight(height);
    }
}
