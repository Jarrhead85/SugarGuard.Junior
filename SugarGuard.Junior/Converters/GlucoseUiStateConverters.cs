using System.Globalization;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Converters;

/// Maps GlucoseUiState → Color resource key string (Normal→Success, Attention→Warning, Critical→Danger)
public class GlucoseUiStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GlucoseUiState state)
        {
            var key = state switch
            {
                GlucoseUiState.Normal => "Success",
                GlucoseUiState.Attention => "Warning",
                GlucoseUiState.Critical => "Danger",
                _ => "Success"
            };
            if (Application.Current?.Resources.TryGetValue(key, out var color) == true)
                return color;
        }
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// Maps GlucoseUiState → localized display text
public class GlucoseUiStateToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is GlucoseUiState state ? state switch
        {
            GlucoseUiState.Normal => "Норма",
            GlucoseUiState.Attention => "Внимание",
            GlucoseUiState.Critical => "Критично",
            _ => "Норма"
        } : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
