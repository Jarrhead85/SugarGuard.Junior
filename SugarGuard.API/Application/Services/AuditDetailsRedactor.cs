using System.Text.RegularExpressions;
using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Whitelist-редактор PHI для AuditLog
/// </summary>
public sealed partial class AuditDetailsRedactor : IAuditDetailsRedactor
{
    private const string Redacted = "[REDACTED]";

    /// <summary>
    /// Ключи, которые разрешено сохранять в AuditLog.details без редактирования.
    /// </summary>
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Child", "Actor", "Role", "Id", "Type"
    };

    /// <summary>
    /// Проверяет Key=Value в цикле. Value может содержать любые символы кроме
    /// </summary>
    [GeneratedRegex(@"(?<key>[A-Za-z][A-Za-z0-9_]*)=(?<value>[^;]*);?", RegexOptions.Singleline)]
    private static partial Regex KeyValueRegex();

    public string? Redact(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return details;
        }

        var matches = KeyValueRegex().Matches(details);
        if (matches.Count == 0)
        {
            return Redacted;
        }

        var sb = new System.Text.StringBuilder(details.Length);
        var lastEnd = 0;
        var anyKept = false;

        foreach (Match m in matches)
        {
            if (m.Index > lastEnd)
            {
                sb.Append(details, lastEnd, m.Index - lastEnd);
            }

            var key = m.Groups["key"].Value;
            var value = m.Groups["value"].Value;

            var valueGroup = m.Groups["value"];
            var hadSemicolon = m.Length > (valueGroup.Index + valueGroup.Length - m.Index);

            if (AllowedKeys.Contains(key))
            {
                // Whitelist: пропускаем Key=Value как есть
                sb.Append(key).Append('=').Append(value);
                if (hadSemicolon) sb.Append(';');
                anyKept = true;
            }
            else
            {
                sb.Append(key).Append('=').Append(Redacted);
                if (hadSemicolon) sb.Append(';');
            }

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < details.Length)
        {
            var tail = details[lastEnd..];
            if (!string.IsNullOrWhiteSpace(tail))
            {
                sb.Append(Redacted);
            }
        }

        var result = sb.ToString();
        return anyKept ? result : string.Empty;
    }
}
