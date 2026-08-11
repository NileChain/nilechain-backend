using NileChain.Domain.Entities;

namespace NileChain.Domain.Common;

/// <summary>
/// Shared list ordering for FarmMatch collections.
/// Default is newest-first by <see cref="FarmMatch.CreatedAt"/> (not delivery date).
/// Stable secondary key: MatchId DESC/ASC matching the primary direction.
/// </summary>
public static class MatchListOrdering
{
    public const string Newest = "newest";
    public const string Oldest = "oldest";
    public const string MatchScoreDesc = "matchScore";
    public const string MatchScoreAsc = "matchScoreAsc";

    public static string Normalize(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return Newest;

        return sort.Trim().ToLowerInvariant() switch
        {
            "oldest" or "createdat_asc" or "created_asc" => Oldest,
            "matchscore" or "score" or "matchscore_desc" or "score_desc" => MatchScoreDesc,
            "matchscoreasc" or "matchscore_asc" or "score_asc" => MatchScoreAsc,
            _ => Newest
        };
    }

    public static IQueryable<FarmMatch> Apply(IQueryable<FarmMatch> query, string? sort)
    {
        return Normalize(sort) switch
        {
            Oldest => query
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.MatchId),
            MatchScoreDesc => query
                .OrderByDescending(m => m.MatchScore)
                .ThenByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MatchId),
            MatchScoreAsc => query
                .OrderBy(m => m.MatchScore)
                .ThenByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MatchId),
            _ => query
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MatchId)
        };
    }
}
