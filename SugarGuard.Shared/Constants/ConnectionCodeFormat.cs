using System.Security.Cryptography;

namespace SugarGuard.Shared.Constants;

/// <summary>
/// Shared 8-symbol connection/verification code format.
/// Display form: ABCD-1234. Stored/normalized form: ABCD1234.
/// </summary>
public static class ConnectionCodeFormat
{
    public const int Length = InviteCodeLimits.CodeLength;
    public static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);

    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";

    public static string Generate()
    {
        var buffer = new byte[Length];
        RandomNumberGenerator.Fill(buffer);

        return string.Create(Length, buffer, (span, buf) =>
        {
            for (var i = 0; i < 4; i++)
                span[i] = Letters[buf[i] % Letters.Length];

            for (var i = 4; i < span.Length; i++)
                span[i] = Digits[buf[i] % Digits.Length];
        });
    }

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var normalized = input
            .Replace(InviteCodeLimits.GroupSeparator.ToString(), string.Empty)
            .Replace('\u2013', InviteCodeLimits.GroupSeparator)
            .Replace('\u2014', InviteCodeLimits.GroupSeparator)
            .Replace('\u2212', InviteCodeLimits.GroupSeparator)
            .Replace(InviteCodeLimits.GroupSeparator.ToString(), string.Empty)
            .Replace(" ", string.Empty)
            .Trim()
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static string Format(string rawCode) => InviteCodeLimits.Format(rawCode);

    public static bool IsValid(string? code, bool normalize = true)
    {
        if (code is null)
            return false;

        var value = normalize ? Normalize(code) : code;
        if (value is null || value.Length != Length)
            return false;

        return value.All(c => Letters.Contains(c) || Digits.Contains(c));
    }
}
