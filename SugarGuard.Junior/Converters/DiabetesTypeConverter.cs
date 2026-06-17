// Конвертер для типа диабета
using System.Globalization;
using SugarGuard.Junior.Models.Enums;

namespace SugarGuard.Junior.Converters;

/// <summary>
/// Конвертирует тип диабета в читаемую строку
/// </summary>
public class DiabetesTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiabetesType diabetesType)
        {
            return diabetesType switch
            {
                DiabetesType.Type1 => "Диабет 1 типа",
                DiabetesType.Type2 => "Диабет 2 типа",
                DiabetesType.LADA => "LADA",
                DiabetesType.Other => "Другой",
                _ => "Неизвестно"
            };
        }
        return "Неизвестно";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return stringValue switch
            {
                "Диабет 1 типа" => DiabetesType.Type1,
                "Диабет 2 типа" => DiabetesType.Type2,
                "LADA" => DiabetesType.LADA,
                "Другой" => DiabetesType.Other,
                _ => DiabetesType.Type1
            };
        }
        return DiabetesType.Type1;
    }
}