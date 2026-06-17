// Утилита парсинга числовых значений с поддержкой разных культур.
//
// Проблема: double.Parse / double.TryParse без явного указания культуры
// использует CurrentCulture. На ru-RU это запятая как разделитель ("4,5"),
// на en-US — точка ("4.5"). Если данные были записаны в одной локали,
// а читаются в другой — парсинг проваливается и возвращается 0 или default.
//
// Решение: пробуем три стратегии в порядке приоритета:
//   1. InvariantCulture (точка) — данные, записанные .ToString(CultureInfo.InvariantCulture)
//   2. CurrentCulture (точка или запятая) — данные, записанные .ToString() в текущей локали
//   3. InvariantCulture после Replace(',', '.') — fallback для случая, когда
//      пользователь ввёл "4,5" на en-US или когда данные были записаны
//      с CurrentCulture, а у пользователя сменилась локаль.
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

        var normalized = rawValue.Trim();

        return
            double.TryParse(
                normalized,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value)
            || double.TryParse(
                normalized,
                NumberStyles.Any,
                CultureInfo.CurrentCulture,
                out value)
            || double.TryParse(
                normalized.Replace(',', '.'),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
    }
}
