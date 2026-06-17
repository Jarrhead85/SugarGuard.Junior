namespace SugarGuard.Junior.Converters;

using System.Globalization;
using SugarGuard.Junior.Models.Enums;

/// <summary>
/// Конвертер: возвращает true, если текущий ScalePreset совпадает с параметром (int) или ScalePreset.
/// Используется для подсветки активной кнопки выбора масштаба в ProfilePage.
/// </summary>
public class ScalePresetToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScalePreset currentPreset)
        {
            var targetPreset = parameter switch
            {
                ScalePreset preset => preset,
                int intVal => (ScalePreset)intVal,
                string strVal => Enum.TryParse<ScalePreset>(strVal, out var parsed) ? parsed : ScalePreset.Default,
                _ => ScalePreset.Default
            };

            return currentPreset == targetPreset;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
