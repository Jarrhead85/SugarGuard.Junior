// Конвертеры для страницы расписания
using System.Globalization;

namespace SugarGuard.Junior.Converters;

/// <summary>
/// Конвертирует строку в булево значение (не пустая строка = true).
/// Предназначен только для одностороннего биндинга (OneWay); не использовать в TwoWay.
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертирует булево значение в текст статуса активности.
/// Только для одностороннего биндинга (OneWay).
/// </summary>
public class BoolToActiveStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? "Активно" : "Отключено";
        }
        return "Неизвестно";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертирует булево значение в цвет статуса активности.
/// Только для одностороннего биндинга (OneWay).
/// </summary>
public class BoolToActiveColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? Colors.Green : Colors.Gray;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертирует булево значение в текст кнопки переключения.
/// Только для одностороннего биндинга (OneWay).
/// </summary>
public class BoolToToggleTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? "Отключить" : "Включить";
        }
        return "Переключить";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертирует булево значение в цвет кнопки переключения.
/// Только для одностороннего биндинга (OneWay).
/// </summary>
public class BoolToToggleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? Colors.Orange : Colors.Green;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертирует булево значение в цвет фона кнопки фильтра.
/// true (активный) → Primary (#1B8E8B), false → прозрачный.
/// Только для одностороннего биндинга (OneWay).
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            if (isActive)
            {
                if (Application.Current?.Resources.TryGetValue("Primary", out var color) == true && color is Color c)
                    return c;
                return Color.FromArgb("#1B8E8B");
            }
            return Colors.Transparent;
        }
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}