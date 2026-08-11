using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Single transactional contract ↔ match state machine.
/// Policy: create / persist / sign only when the match is <see cref="FarmMatchStatus.Proposed"/>.
/// Regenerating text on an <see cref="FarmMatchStatus.Accepted"/> match atomically reopens it to Proposed
/// (signatures cleared). Rejected / Expired matches never reopen — callers must fail cleanly.
/// </summary>
public static class ContractExecution
{
    /// <summary>
    /// True when a new contract may be created for this match (must be Proposed).
    /// </summary>
    public static bool CanCreateContract(FarmMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return match.Status == FarmMatchStatus.Proposed;
    }

    /// <summary>
    /// True when a party may sign the contract (match must still be Proposed).
    /// </summary>
    public static bool CanSign(FarmMatch? match) =>
        match is { Status: FarmMatchStatus.Proposed };

    /// <summary>
    /// True when generated text may be replaced (Proposed, or Accepted which will reopen).
    /// </summary>
    public static bool CanReplaceText(FarmMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return match.Status is FarmMatchStatus.Proposed or FarmMatchStatus.Accepted;
    }

    /// <summary>
    /// Overwrite contract body, invalidate signatures, set PendingSignature.
    /// If match was Accepted, reopen to Proposed so the pair stays consistent.
    /// Returns false when match is Rejected/Expired (caller must not persist).
    /// </summary>
    public static bool TryReplaceGeneratedText(Contract contract, FarmMatch match, string contractText)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractText);

        if (!CanReplaceText(match))
            return false;

        if (match.Status == FarmMatchStatus.Accepted)
            match.Status = FarmMatchStatus.Proposed;

        contract.GeneratedText = contractText;
        contract.ClearSignatures();
        contract.Status = ContractStatus.PendingSignature;
        return true;
    }

    /// <summary>
    /// Legacy helper for unit tests that already have a Proposed match attached.
    /// Prefer <see cref="TryReplaceGeneratedText"/>.
    /// </summary>
    public static void ReplaceGeneratedText(Contract contract, string contractText)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var match = contract.FarmMatch
            ?? throw new InvalidOperationException(
                "ReplaceGeneratedText requires contract.FarmMatch to enforce match status.");

        if (!TryReplaceGeneratedText(contract, match, contractText))
            throw new InvalidOperationException(
                $"Cannot replace contract text when match status is {match.Status}.");
    }

    /// <summary>
    /// When both parties have signed, mark the related Proposed match as Accepted.
    /// </summary>
    public static void AcceptMatchIfFullySigned(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.IsFullySigned)
            return;

        if (contract.FarmMatch is { Status: FarmMatchStatus.Proposed } match)
            match.Status = FarmMatchStatus.Accepted;
    }

    /// <summary>
    /// When a contract is rejected/cancelled, mark a Proposed match as Rejected.
    /// </summary>
    public static void RejectMatchIfProposed(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.FarmMatch is { Status: FarmMatchStatus.Proposed } match)
            match.Status = FarmMatchStatus.Rejected;
    }
}
