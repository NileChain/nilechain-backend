namespace NileChain.AI.Models;

/// <summary>
/// Matching shortlist with visible truncation metadata (Take-N cap).
/// </summary>
public class MatchSearchResult
{
    public List<MatchResult> Results { get; set; } = new();

    /// <summary>Eligible farms after crop + geo + active filters, before Take.</summary>
    public int TotalEligible { get; set; }

    /// <summary>Farms ranked but not returned due to the Take cap (TotalEligible - Results.Count).</summary>
    public int TruncatedCount { get; set; }

    /// <summary>Configured shortlist size used for this search.</summary>
    public int TakeLimit { get; set; }

    /// <summary>Better farm one ring outside the committed scope, if any. Not in <see cref="Results"/>.</summary>
    public PeekHint? PeekHint { get; set; }
}
