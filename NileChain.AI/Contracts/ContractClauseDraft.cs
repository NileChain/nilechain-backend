using System.Text.Json;
using System.Text.RegularExpressions;

namespace NileChain.AI.Contracts;

/// <summary>
/// Legal prose the model is allowed to author, keyed by article
/// (see <see cref="ContractArticles.Keys"/>). Anything rejected by
/// <see cref="TryParse"/> simply falls back to the deterministic wording.
/// </summary>
public sealed record ContractClauseDraft(IReadOnlyDictionary<string, string> Bodies)
{
    /// <summary>Longest prose accepted per article; a runaway answer is not a clause.</summary>
    private const int MaxBodyLength = 1_200;

    private const int MinBodyLength = 40;

    /// <summary>
    /// Digits are the whole point of the guard: the agreed numbers live in the article's
    /// fact line, so any figure in model prose can only contradict the deal.
    /// Covers ASCII, Arabic-Indic, and Extended Arabic-Indic digits.
    /// </summary>
    private static readonly Regex Digits = new(
        @"[0-9\u0660-\u0669\u06F0-\u06F9]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Bracketed placeholders and handwritten signature slots must never survive.</summary>
    private static readonly Regex Forbidden = new(
        @"[\[\]{}]|التوقيع\s*[:：]|الشهود|توقيع\s*الطرف",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex JsonFence = new(
        @"^\s*```(?:json)?\s*|\s*```\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static ContractClauseDraft Empty { get; } =
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public bool IsEmpty => Bodies.Count == 0;

    public string? Body(string key) =>
        Bodies.TryGetValue(key, out var body) ? body : null;

    /// <summary>
    /// Reads the model's JSON answer, keeping only the articles whose prose is
    /// recognised, digit-free, and free of placeholder or signature markup.
    /// Returns false when nothing usable came back.
    /// </summary>
    public static bool TryParse(string? raw, out ContractClauseDraft draft, out string? rejectionReason)
    {
        draft = Empty;
        rejectionReason = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            rejectionReason = "empty response";
            return false;
        }

        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            rejectionReason = "no JSON object in response";
            return false;
        }

        Dictionary<string, string> accepted = new(StringComparer.OrdinalIgnoreCase);
        var rejected = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                rejectionReason = "JSON root is not an object";
                return false;
            }

            foreach (var key in ContractArticles.Keys)
            {
                if (!doc.RootElement.TryGetProperty(key, out var prop)
                    || prop.ValueKind != JsonValueKind.String)
                    continue;

                var body = Normalize(prop.GetString());
                if (body is null)
                {
                    rejected.Add(key);
                    continue;
                }

                accepted[key] = body;
            }
        }
        catch (JsonException ex)
        {
            rejectionReason = $"malformed JSON: {ex.Message}";
            return false;
        }

        if (accepted.Count == 0)
        {
            rejectionReason = rejected.Count > 0
                ? $"all clauses rejected by the numeric guard ({string.Join(", ", rejected)})"
                : "no recognised clause keys";
            return false;
        }

        if (rejected.Count > 0)
            rejectionReason = $"clauses rejected by the numeric guard: {string.Join(", ", rejected)}";

        draft = new ContractClauseDraft(accepted);
        return true;
    }

    private static string? Normalize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var text = body.Trim();
        if (text.Length is < MinBodyLength or > MaxBodyLength)
            return null;

        if (Digits.IsMatch(text) || Forbidden.IsMatch(text))
            return null;

        return text;
    }

    /// <summary>Models wrap JSON in prose or fences often enough that trimming is cheaper than retrying.</summary>
    private static string? ExtractJsonObject(string raw)
    {
        var text = JsonFence.Replace(raw.Trim(), string.Empty).Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }
}
