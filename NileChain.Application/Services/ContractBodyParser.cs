using System.Text.RegularExpressions;
using NileChain.Domain.Common;

namespace NileChain.Application.Services;

/// <summary>
/// Parses <c>GeneratedText</c> into numbered legal sections for PDF/web-aligned rendering.
/// Does not invent clause titles that are absent from the source.
/// </summary>
public static partial class ContractBodyParser
{
    public sealed record Section(string Title, IReadOnlyList<string> Paragraphs);

    public static bool IsMostlyArabic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var arabic = 0;
        var latin = 0;
        foreach (var ch in text)
        {
            if (ch is >= '\u0600' and <= '\u06FF')
                arabic++;
            else if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))
                latin++;
        }

        return arabic > latin;
    }

    public static string? ExtractBismillah(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var plain = StripInlineMarkdown(line.Trim());
            if (string.IsNullOrWhiteSpace(plain))
                continue;
            return BismillahRegex().IsMatch(plain) ? plain : null;
        }

        return null;
    }

    /// <summary>
    /// Best-effort payment-terms snippet from existing generated prose.
    /// Returns null when no payment wording is present — never invents terms.
    /// </summary>
    public static string? ExtractPaymentTermsHint(string? raw)
    {
        var text = ContractSignatureText.StripHandwrittenBlocks(raw);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var plain = StripInlineMarkdown(line.Trim());
            if (plain.Length < 12 || plain.Length > 220)
                continue;
            if (HeadingRegex().IsMatch(plain))
                continue;
            if (PaymentLineRegex().IsMatch(plain))
                return plain;
        }

        return null;
    }

    public static IReadOnlyList<Section> ParseSections(string? raw)
    {
        var text = ContractSignatureText.StripHandwrittenBlocks(raw);
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<Section>();

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sections = new List<Section>();
        string? currentTitle = null;
        var buffer = new List<string>();

        void Flush()
        {
            if (currentTitle is null && buffer.Count == 0)
                return;

            var joined = string.Join("\n", buffer).Trim();
            var paragraphs = string.IsNullOrWhiteSpace(joined)
                ? Array.Empty<string>()
                : joined.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => StripInlineMarkdown(p.Trim()))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

            // Skip empty bismillah-only / title-only leading blocks with no body when later sections exist.
            if ((currentTitle is null || string.IsNullOrWhiteSpace(currentTitle)) && paragraphs.Length == 0)
            {
                buffer.Clear();
                return;
            }

            sections.Add(new Section(currentTitle ?? string.Empty, paragraphs));
            buffer.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                buffer.Add(string.Empty);
                continue;
            }

            var plain = StripInlineMarkdown(trimmed);

            // Document chrome (bismillah / contract title) is not a legal clause heading.
            if (BismillahRegex().IsMatch(plain) || ContractTitleRegex().IsMatch(plain)
                || plain.Equals("Agricultural Supply Agreement", StringComparison.OrdinalIgnoreCase)
                || plain.Equals("Agricultural Supply Contract", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsHeading(plain) || IsHeading(trimmed))
            {
                Flush();
                currentTitle = LeadingNumberRegex().Replace(plain, string.Empty).Trim();
                continue;
            }

            if (currentTitle is null && sections.Count == 0 && buffer.Count == 0)
            {
                // Leading prose (preamble) under an empty title — do not invent an article name.
                currentTitle = string.Empty;
            }

            buffer.Add(plain);
        }

        Flush();

        if (sections.Count == 0)
        {
            var paragraphs = text
                .Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => StripInlineMarkdown(p.Trim()))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
            return new[] { new Section(string.Empty, paragraphs) };
        }

        // Drop a leading section that is only the contract title / bismillah.
        if (sections.Count > 1)
        {
            var first = sections[0];
            var titleOnly =
                first.Paragraphs.Count == 0
                && (string.IsNullOrWhiteSpace(first.Title)
                    || BismillahRegex().IsMatch(first.Title)
                    || ContractTitleRegex().IsMatch(first.Title));
            var bismillahBody =
                first.Paragraphs.Count <= 1
                && first.Paragraphs.All(p => BismillahRegex().IsMatch(p) || ContractTitleRegex().IsMatch(p));
            if (titleOnly || bismillahBody)
                sections.RemoveAt(0);
        }

        return sections;
    }

    private static bool IsHeading(string plain)
    {
        if (string.IsNullOrWhiteSpace(plain))
            return false;
        if (BismillahRegex().IsMatch(plain) || ContractTitleRegex().IsMatch(plain))
            return false;
        if (HeadingRegex().IsMatch(plain))
            return true;
        if (plain.Length > 80 || plain.EndsWith('.') || plain.Contains("EGP", StringComparison.OrdinalIgnoreCase))
            return false;
        if (LatinTitleRegex().IsMatch(plain) && plain.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 8
            && !plain.Equals("Agricultural Supply Agreement", StringComparison.OrdinalIgnoreCase)
            && !plain.Equals("Agricultural Supply Contract", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string StripInlineMarkdown(string value) =>
        value
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .TrimStart('#', ' ', '\t')
            .Trim();

    [GeneratedRegex(@"^بسم\s+الله", RegexOptions.CultureInvariant)]
    private static partial Regex BismillahRegex();

    [GeneratedRegex(@"^عقد\s+توريد", RegexOptions.CultureInvariant)]
    private static partial Regex ContractTitleRegex();

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9 ,/\-]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex LatinTitleRegex();

    [GeneratedRegex(@"^\d{1,2}[\.\-\)]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumberRegex();

    [GeneratedRegex(
        @"^(?:#{1,3}\s+)|^(?:\d{1,2}[\.\-\)]\s+)|^(?:Article\s+\d+[:.\-\s]+)|^(?:Section\s+\d+[:.\-\s]+)|^(?:Clause\s+\d+[:.\-\s]+)|^(?:البند\s*.+)|^(?:المادة\s*.+)|^(?:أولا[ًا]?|ثانيا[ًا]?|ثالثا[ًا]?|رابعا[ًا]?|خامسا[ًا]?|سادسا[ًا]?|سابعا[ًا]?|ثامنا[ًا]?|تاسعا[ًا]?|عاشرا[ًا]?)[:.\-\s]|^(?:شروط\s+(?:التسليم|الدفع|الجودة|العقد))|^(?:التزامات\s+(?:المصنع|المزرعة|الطرف))|^(?:فض\s+النزاعات|حل\s+النزاعات|القانون\s+المطبق)|^(?:مدة\s+العقد|إنهاء\s+العقد|Parties|Payment|Delivery|Quality)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(
        @"(الدفع|السداد|التسوية|Payment|paid|settlement|invoice|مقدما| مقدماً|عند الاستلام)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PaymentLineRegex();
}
