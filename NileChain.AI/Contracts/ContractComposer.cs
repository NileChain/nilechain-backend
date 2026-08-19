using System.Text;

namespace NileChain.AI.Contracts;

/// <summary>
/// Renders the final Arabic contract. The skeleton, the party names, and every figure come
/// from <see cref="ContractFacts"/>; a model can only contribute the legal prose of an
/// article, and only after <see cref="ContractClauseDraft"/> has cleared it.
/// Signature blocks are intentionally omitted — the platform renders e-signatures.
/// </summary>
public static class ContractComposer
{
    public static string Compose(ContractFacts facts, ContractClauseDraft? clauses = null)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var sb = new StringBuilder();
        sb.AppendLine(ContractArticles.Bismillah);
        sb.AppendLine();
        sb.AppendLine(ContractArticles.Title);
        sb.AppendLine();
        sb.AppendLine(ContractArticles.Preamble(facts));

        foreach (var article in ContractArticles.Build(facts))
        {
            sb.AppendLine();
            sb.AppendLine(article.Title);

            if (!string.IsNullOrWhiteSpace(article.FactLine))
                sb.AppendLine(article.FactLine);

            sb.AppendLine(clauses?.Body(article.Key) ?? article.DefaultBody);
        }

        return sb.ToString();
    }

    /// <summary>How many articles a model draft actually supplied — useful for run trails.</summary>
    public static int AuthoredClauseCount(ContractClauseDraft? clauses) =>
        clauses?.Bodies.Count ?? 0;
}
