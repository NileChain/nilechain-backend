using System.Text.RegularExpressions;

namespace NileChain.Domain.Common;

/// <summary>
/// Removes handwritten signature blanks from generated contract prose.
/// Platform e-signature UI/PDF blocks are the source of truth for signing state.
/// </summary>
public static partial class ContractSignatureText
{
    public static string StripHandwrittenBlocks(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var kept = new List<string>(lines.Length);
        var dropping = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (IsSignatureHeading(trimmed))
            {
                dropping = true;
                continue;
            }

            if (dropping)
            {
                // Stop dropping once a new legal article/section starts.
                if (IsLegalSectionResume(trimmed))
                {
                    dropping = false;
                }
                else if (string.IsNullOrWhiteSpace(trimmed) || IsSignatureLine(trimmed))
                {
                    continue;
                }
                else if (LooksLikeSignaturePartyBlock(trimmed))
                {
                    continue;
                }
                else
                {
                    // Unknown prose after signature heading — keep dropping trailing blanks only.
                    continue;
                }
            }

            if (!dropping && IsSignatureLine(trimmed))
                continue;

            kept.Add(line);
        }

        var result = string.Join("\n", kept).Trim();
        // Collapse excessive blank lines created by removals.
        result = MultiBlankRegex().Replace(result, "\n\n");
        return result;
    }

    private static bool IsSignatureHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return SignatureHeadingRegex().IsMatch(line);
    }

    private static bool IsLegalSectionResume(string line)
    {
        return ArticleHeadingRegex().IsMatch(line);
    }

    private static bool IsSignatureLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (SignatureBlankRegex().IsMatch(line))
            return true;

        if (UnderscoreBlankRegex().IsMatch(line) &&
            (line.Contains("توقيع", StringComparison.Ordinal) ||
             line.Contains("التاريخ", StringComparison.Ordinal) ||
             line.Contains("Signature", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("Date", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeSignaturePartyBlock(string line)
    {
        return PartySignatureBlockRegex().IsMatch(line);
    }

    [GeneratedRegex(
        @"^(?:#{1,3}\s*)?(?:التوقيعات|خانات\s*التوقيع|Signatures?|Signature\s*blocks?)\s*:?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SignatureHeadingRegex();

    [GeneratedRegex(
        @"^(?:المادة|البند|أولا|ثانيا|ثالثا|رابعا|خامسا|سادسا|سابعا|ثامنا|تاسعا|Article|Section|Clause)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArticleHeadingRegex();

    [GeneratedRegex(
        @"^(?:توقيع\s*(?:المورد|المشتري|الطرف|المصنع|المزرعة|الأول|الثاني)|(?:التوقيع|التاريخ)\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SignatureBlankRegex();

    [GeneratedRegex(@"_{3,}|\.{3,}|…{1,}|ـ{3,}")]
    private static partial Regex UnderscoreBlankRegex();

    [GeneratedRegex(
        @"^الطرف\s*(?:الأول|الثاني)\s*:.*(?:توقيع|التاريخ)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartySignatureBlockRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiBlankRegex();
}
