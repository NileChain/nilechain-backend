using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

public static class MatchMessaging
{
    public const int MaxNegotiationRounds = 8;

    public static bool CanMessage(FarmMatch match) =>
        !match.IsExcludedByFactory
        && match.Status is FarmMatchStatus.Proposed
            or FarmMatchStatus.Countered
            or FarmMatchStatus.Accepted;

    public static bool CanNegotiate(FarmMatch match) =>
        !match.IsExcludedByFactory
        && match.Status is FarmMatchStatus.Proposed or FarmMatchStatus.Countered;

    public static bool CanAddRound(FarmMatch match) =>
        (match.NegotiationRounds?.Count ?? 0) < MaxNegotiationRounds;

    public static DealParty? LastOfferedBy(FarmMatch match) =>
        match.NegotiationRounds?
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.RoundId)
            .Select(r => (DealParty?)r.OfferedBy)
            .FirstOrDefault();

    /// <summary>
    /// Accept the other party's latest commercial counter. Legacy farm-only counters
    /// (no round rows) remain factory-accept only.
    /// </summary>
    public static bool CanAcceptCounter(FarmMatch match, DealParty acceptor)
    {
        if (!CanNegotiate(match) || match.Status != FarmMatchStatus.Countered)
            return false;

        if (!MatchCommercialTerms.HasCounter(match))
            return false;

        var last = LastOfferedBy(match);
        if (last is null)
            return acceptor == DealParty.Factory;

        return last.Value != acceptor;
    }
}
