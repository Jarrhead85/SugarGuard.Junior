using System.Text;

namespace SugarGuard.Junior.Core.Validation;

public static class GlucoseInputNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var result = new StringBuilder(value.Length);
        var hasDecimalSeparator = false;

        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                result.Append(character);
                continue;
            }

            if ((character == '.' || character == ',') && !hasDecimalSeparator)
            {
                result.Append('.');
                hasDecimalSeparator = true;
            }
        }

        return result.ToString();
    }
}
