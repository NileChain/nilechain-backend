using System.Text.RegularExpressions;

namespace NileChain.Application.Validation.Common;

/// <summary>
/// Egyptian mobile phone normalization and validation (local 01xxxxxxxxx format).
/// </summary>
public static partial class EgyptianPhone
{
    public const string InvalidMessage =
        "Phone number must be an Egyptian mobile number: 11 digits starting with 01.";

    /// <summary>
    /// Strips spaces, dashes, parentheses; converts +20 / 0020 to local 0-prefix form.
    /// Returns digits-only candidate (may still be invalid).
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        s = s.Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal);

        if (s.StartsWith("+20", StringComparison.Ordinal))
            s = "0" + s[3..];
        else if (s.StartsWith("0020", StringComparison.Ordinal))
            s = "0" + s[4..];
        else if (s.StartsWith("20", StringComparison.Ordinal) && s.Length == 12)
            s = "0" + s[2..];

        return NonDigits().Replace(s, string.Empty);
    }

    public static bool IsValid(string? raw)
    {
        var normalized = Normalize(raw);
        return LocalMobile().IsMatch(normalized);
    }

    public static bool TryNormalizeValid(string? raw, out string normalized)
    {
        normalized = Normalize(raw);
        return LocalMobile().IsMatch(normalized);
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^01\d{9}$")]
    private static partial Regex LocalMobile();
}
