namespace SugarGuard.Junior.Converters;

using System.Globalization;
using SugarGuard.Junior.Models.Enums;

/// <summary>
/// Конвертер: IsSynced == false → true (показывать индикатор ожидающей синхронизации).
/// </summary>
public class PendingSyncToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSynced)
            return !isSynced;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
