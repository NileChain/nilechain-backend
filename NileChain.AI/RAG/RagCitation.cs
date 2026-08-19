using System.Text;
using System.Text.RegularExpressions;

namespace NileChain.AI.RAG;

/// <summary>One retrieved knowledge-base passage, before it is numbered for citation.</summary>
public sealed record RagChunk(string Id, string Title, string Document)
{
    public const string UnknownTitle = "مصدر غير مُسمّى";
}

/// <summary>A numbered source the model is allowed to cite as <c>[Index]</c>.</summary>
public sealed record RagCitation(int Index, string Id, string Section, string Title, string Excerpt)
{
    private const int ExcerptLength = 320;

    public static string Shorten(string document) =>
        document.Length <= ExcerptLength
            ? document.Trim()
            : document.Trim()[..ExcerptLength] + "…";
}

/// <summary>
/// Retrieval result carrying both renderings of the same passages: <see cref="CitedText"/> for
/// answers that must show their sources, and <see cref="PlainText"/> for the contract path, where
/// bracketed markers are forbidden.
/// </summary>
public sealed record RagContext(
    bool IsAvailable,
    string PlainText,
    string CitedText,
    IReadOnlyList<RagCitation> Citations)
{
    /// <summary>Marker the model is told to use, e.g. <c>[2]</c>.</summary>
    private static readonly Regex Marker = new(
        @"\[(\d{1,2})\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool HasKnowledge => IsAvailable && Citations.Count > 0;

    public static RagContext Unavailable(string message) =>
        new(false, message, message, []);

    public static RagContext Empty() =>
        new(true, string.Empty, string.Empty, []);

    public static RagContext FromSections(IReadOnlyList<(string Section, RagChunk Chunk)> passages)
    {
        if (passages.Count == 0)
            return Empty();

        var citations = new List<RagCitation>(passages.Count);
        var plain = new StringBuilder();
        var cited = new StringBuilder();

        for (var i = 0; i < passages.Count; i++)
        {
            var (section, chunk) = passages[i];
            var index = i + 1;

            citations.Add(new RagCitation(
                index,
                chunk.Id,
                section,
                chunk.Title,
                RagCitation.Shorten(chunk.Document)));

            if (plain.Length > 0)
            {
                plain.AppendLine().AppendLine();
                cited.AppendLine().AppendLine();
            }

            plain.Append(chunk.Document.Trim());
            cited.Append($"[{index}] {section} — {chunk.Title}")
                .AppendLine()
                .Append(chunk.Document.Trim());
        }

        return new RagContext(true, plain.ToString(), cited.ToString(), citations);
    }

    /// <summary>
    /// Keeps only the citations the answer actually referenced, and drops markers pointing at
    /// sources that were never retrieved — a dangling <c>[7]</c> is a fabricated source.
    /// </summary>
    public (string Answer, IReadOnlyList<RagCitation> Used) ResolveCitations(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer) || Citations.Count == 0)
            return (Marker.Replace(answer ?? string.Empty, string.Empty).Trim(), []);

        var used = new List<RagCitation>();

        var cleaned = Marker.Replace(answer, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out var index))
                return string.Empty;

            var citation = Citations.FirstOrDefault(c => c.Index == index);
            if (citation is null)
                return string.Empty;

            if (used.All(c => c.Index != index))
                used.Add(citation);

            return match.Value;
        });

        return (cleaned.Trim(), used.OrderBy(c => c.Index).ToList());
    }
}
