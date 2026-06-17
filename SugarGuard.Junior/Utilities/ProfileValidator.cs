// Валидатор для данных профиля ребёнка
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;

namespace SugarGuard.Junior.Utilities;

/// <summary>
/// Валидатор для профиля ребёнка и настроек диабета
/// Объединяет все проверки в одном месте согласно Requirements 7.1
/// </summary>
public static class ProfileValidator
{
    /// <summary>
    /// Результат валидации профиля
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        
        public ValidationResult(bool isValid = true)
        {
            IsValid = isValid;
        }
        
        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }

    /// <summary>
    /// Валидирует полный профиль ребёнка
    /// Проверяет: вес 10-200 кг, рост 60-200 см, возраст 4-18 лет, тип диабета 1 или 2
    /// </summary>
    /// <param name="child">Профиль ребёнка для валидации</param>
    /// <returns>Результат валидации с ошибками</returns>
    public static ValidationResult ValidateChildProfile(Child child)
    {
        var result = new ValidationResult();

        // Проверка имени (используем зашифрованное поле как источник незашифрованных данных)
        if (string.IsNullOrWhiteSpace(child.EncryptedFirstName))
        {
            result.AddError("Имя не может быть пустым");
        }
        else if (child.EncryptedFirstName.Length < 2)
        {
            result.AddError("Имя должно содержать минимум 2 символа");
        }

        // Проверка фамилии (используем зашифрованное поле как источник незашифрованных данных)
        if (string.IsNullOrWhiteSpace(child.EncryptedLastName))
        {
            result.AddError("Фамилия не может быть пустой");
        }
        else if (child.EncryptedLastName.Length < 2)
        {
            result.AddError("Фамилия должна содержать минимум 2 символа");
        }

        // Проверка возраста (4-18 лет)
        var age = child.AgeInYears;
        if (!ChildProfileLimits.IsValidAge(age))
        {
            result.AddError($"Возраст должен быть от {ChildProfileLimits.AgeMin} до {ChildProfileLimits.AgeMax} лет (текущий: {age})");
        }

        // Проверка веса (10-200 кг)
        if (!ChildProfileLimits.IsValidWeight(child.Weight))
        {
            result.AddError($"Вес должен быть от {ChildProfileLimits.WeightMin} до {ChildProfileLimits.WeightMax} кг");
        }

        // Проверка роста (60-200 см)
        if (!ChildProfileLimits.IsValidHeight(child.Height))
        {
            result.AddError($"Рост должен быть от {ChildProfileLimits.HeightMin} до {ChildProfileLimits.HeightMax} см");
        }

        // Проверка типа диабета (1 или 2)
        if (child.DiabetesType != DiabetesType.Type1 && child.DiabetesType != DiabetesType.Type2)
        {
            result.AddError("Тип диабета должен быть 1 или 2");
        }

        // Проверка даты диагноза (не в будущем)
        if (child.DiagnosisDate > DateTime.Today)
        {
            result.AddError("Дата диагноза не может быть в будущем");
        }

        // Проверка даты рождения (не в будущем)
        if (child.DateOfBirth > DateTime.Today)
        {
            result.AddError("Дата рождения не может быть в будущем");
        }

        return result;
    }

    /// <summary>
    /// Валидирует настройки диабета
    /// Принимает расшифрованные значения для валидации
    /// </summary>
    /// <param name="targetRangeMin">Минимальный целевой уровень глюкозы (расшифрованный)</param>
    /// <param name="targetRangeMax">Максимальный целевой уровень глюкозы (расшифрованный)</param>
    /// <param name="insulinSensitivity">Чувствительность к инсулину (расшифрованная)</param>
    /// <param name="carbInsulinRatio">Коэффициент углеводов-инсулина (расшифрованный)</param>
    /// <param name="longActingDuration">Длительность действия длительного инсулина (часов)</param>
    /// <param name="shortActingDuration">Длительность действия быстрого инсулина (часов)</param>
    /// <returns>Результат валидации</returns>
    public static ValidationResult ValidateDiabetesSettings(
        double targetRangeMin,
        double targetRangeMax,
        double insulinSensitivity,
        double carbInsulinRatio,
        int longActingDuration,
        int shortActingDuration)
    {
        var result = new ValidationResult();

        // Проверка целевого диапазона
        if (targetRangeMin <= 0 || targetRangeMin > 30)
        {
            result.AddError("Минимальный целевой уровень должен быть от 0.1 до 30.0 ммоль/л");
        }

        if (targetRangeMax <= 0 || targetRangeMax > 30)
        {
            result.AddError("Максимальный целевой уровень должен быть от 0.1 до 30.0 ммоль/л");
        }

        if (targetRangeMin >= targetRangeMax)
        {
            result.AddError("Минимальный уровень должен быть меньше максимального");
        }

        // Проверка чувствительности к инсулину
        if (insulinSensitivity <= 0 || insulinSensitivity > 10)
        {
            result.AddError("Чувствительность к инсулину должна быть от 0.1 до 10.0 ммоль/л на единицу");
        }

        // Проверка коэффициента углеводов-инсулина
        if (carbInsulinRatio <= 0 || carbInsulinRatio > 100)
        {
            result.AddError("Коэффициент углеводов-инсулина должен быть от 0.1 до 100.0 г на единицу");
        }

        // Проверка длительности действия инсулинов
        if (longActingDuration < 12 || longActingDuration > 48)
        {
            result.AddError("Длительность действия длительного инсулина должна быть от 12 до 48 часов");
        }

        if (shortActingDuration < 2 || shortActingDuration > 8)
        {
            result.AddError("Длительность действия быстрого инсулина должна быть от 2 до 8 часов");
        }

        return result;
    }

    /// <summary>
    /// Валидирует отдельные поля профиля (для использования в UI)
    /// </summary>
    public static class FieldValidators
    {
        /// <summary>
        /// Проверяет корректность веса
        /// </summary>
        public static (bool isValid, string? error) ValidateWeight(double weight)
        {
            if (!ChildProfileLimits.IsValidWeight(weight))
            {
                return (false, $"Вес должен быть от {ChildProfileLimits.WeightMin} до {ChildProfileLimits.WeightMax} кг");
            }
            return (true, null);
        }

        /// <summary>
        /// Проверяет корректность роста
        /// </summary>
        public static (bool isValid, string? error) ValidateHeight(double height)
        {
            if (!ChildProfileLimits.IsValidHeight(height))
            {
                return (false, $"Рост должен быть от {ChildProfileLimits.HeightMin} до {ChildProfileLimits.HeightMax} см");
            }
            return (true, null);
        }

        /// <summary>
        /// Проверяет корректность возраста по дате рождения
        /// </summary>
        public static (bool isValid, string? error) ValidateDateOfBirth(DateTime dateOfBirth)
        {
            if (dateOfBirth > DateTime.Today)
            {
                return (false, "Дата рождения не может быть в будущем");
            }

            var age = DateTime.Today.Year - dateOfBirth.Year;
            if (dateOfBirth > DateTime.Today.AddYears(-age))
                age--;

            if (!ChildProfileLimits.IsValidAge(age))
            {
                return (false, $"Возраст должен быть от {ChildProfileLimits.AgeMin} до {ChildProfileLimits.AgeMax} лет");
            }

            return (true, null);
        }

        /// <summary>
        /// Вычисляет ИМТ по весу и росту
        /// </summary>
        public static double CalculateBMI(double weight, double height)
        {
            if (height <= 0) return 0;
            return Math.Round(weight / Math.Pow(height / 100.0, 2), 2);
        }
    }
}