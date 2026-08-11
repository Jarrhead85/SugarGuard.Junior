namespace SugarGuard.API.Application.Services;

/// <summary>
/// Encodes untrusted text for spreadsheet-compatible CSV files and prevents
/// formula execution when the file is opened in Excel or similar software.
/// </summary>
internal static class CsvCellEncoder
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    public static string Encode(string? value, char delimiter = ',', bool alwaysQuote = false)
    {
        if (string.IsNullOrEmpty(value))
            return alwaysQuote ? "\"\"" : string.Empty;

        var safeValue = RequiresFormulaNeutralization(value)
            ? $"'{value}"
            : value;

        if (!alwaysQuote
            && !safeValue.Contains(delimiter)
            && !safeValue.Contains('"')
            && !safeValue.Contains('\n')
            && !safeValue.Contains('\r'))
        {
            return safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static bool RequiresFormulaNeutralization(string value)
    {
        if (value[0] is '\t' or '\r' or '\n')
            return true;

        var firstNonWhitespace = 0;
        while (firstNonWhitespace < value.Length && char.IsWhiteSpace(value[firstNonWhitespace]))
            firstNonWhitespace++;

        return firstNonWhitespace < value.Length
               && FormulaPrefixes.Contains(value[firstNonWhitespace]);
    }
}
