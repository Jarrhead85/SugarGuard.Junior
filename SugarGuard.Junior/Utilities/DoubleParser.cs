// Утилита парсинга числовых значений с поддержкой разных культур.
//
// Проблема: double.Parse / double.TryParse без явного указания культуры
// использует CurrentCulture. На ru-RU это запятая как разделитель ("4,5"),
// на en-US — точка ("4.5"). Если данные были записаны в одной локали,
// а читаются в другой — парсинг проваливается и возвращается 0 или default.
//
// Решение: нормализуем десятичный разделитель и парсим только как Float.
// Важно не использовать NumberStyles.Any с CurrentCulture: на ru-RU строка "5.0"
// может быть истолкована как "50" из-за точки как разделителя тысяч.
//
// Все методы — TryParse (без исключений), безопасны для hot path при чтении
// расшифрованных значений из БД.

using System.Globalization;

namespace SugarGuard.Junior.Utilities;

public static class DoubleParser
{
    /// <summary>
    /// Парсит строку как <see cref="double"/> с поддержкой InvariantCulture,
    /// текущей локали и точки/запятой как десятичного разделителя.
    /// </summary>
    /// <param name="rawValue">Исходная строка (может быть null или пустой).</param>
    /// <param name="value">Распарсенное значение или 0, если парсинг не удался.</param>
    /// <returns><c>true</c> если строка валидна, иначе <c>false</c>.</returns>
    public static bool TryParseDecrypted(string? rawValue, out double value)
    {
        value = 0d;

        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        var normalized = rawValue
            .Trim()
            .Replace("\u00A0", string.Empty)
            .Replace(" ", string.Empty);

        if (double.TryParse(
                normalized.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
        {
            return true;
        }

        if (double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value))
        {
            return true;
        }

        value = 0d;
        return false;
    }
}
