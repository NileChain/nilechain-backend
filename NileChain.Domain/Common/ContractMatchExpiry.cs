using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Pure selection helpers for ageing Proposed matches and unsigned contracts.
/// </summary>
public static class ContractMatchExpiry
{
    public const int DefaultExpiryDays = 14;

    public static bool IsProposedMatchExpired(
        FarmMatchStatus status,
        DateTime createdAtUtc,
        DateTime utcNow,
        int expiryDays = DefaultExpiryDays) =>
        IsOpenMatchExpired(status, createdAtUtc, utcNow, expiryDays);

    /// <summary>Proposed and Countered matches age out; Accepted/Rejected/Expired do not.</summary>
    public static bool IsOpenMatchExpired(
        FarmMatchStatus status,
        DateTime createdAtUtc,
        DateTime utcNow,
        int expiryDays = DefaultExpiryDays)
    {
        if (status is not (FarmMatchStatus.Proposed or FarmMatchStatus.Countered))
            return false;

        var days = Math.Max(1, expiryDays);
        return createdAtUtc <= utcNow.AddDays(-days);
    }

    public static bool IsPendingContractExpired(
        ContractStatus status,
        DateTime createdAtUtc,
        DateTime utcNow,
        int expiryDays = DefaultExpiryDays)
    {
        if (status is not (
                ContractStatus.Draft
                or ContractStatus.PendingSignature
                or ContractStatus.PendingFarmSignature
                or ContractStatus.PendingFactorySignature))
        {
            return false;
        }

        var days = Math.Max(1, expiryDays);
        return createdAtUtc <= utcNow.AddDays(-days);
    }

    /// <summary>
    /// Filters match ids that should become <see cref="FarmMatchStatus.Expired"/>.
    /// </summary>
    public static IReadOnlyList<T> SelectExpiredProposedMatches<T>(
        IEnumerable<T> matches,
        Func<T, FarmMatchStatus> status,
        Func<T, DateTime> createdAtUtc,
        DateTime utcNow,
        int expiryDays = DefaultExpiryDays) =>
        matches
            .Where(m => IsOpenMatchExpired(status(m), createdAtUtc(m), utcNow, expiryDays))
            .ToList();

    /// <summary>
    /// Filters contracts that should become <see cref="ContractStatus.Cancelled"/>.
    /// </summary>
    public static IReadOnlyList<T> SelectExpiredPendingContracts<T>(
        IEnumerable<T> contracts,
        Func<T, ContractStatus> status,
        Func<T, DateTime> createdAtUtc,
        DateTime utcNow,
        int expiryDays = DefaultExpiryDays) =>
        contracts
            .Where(c => IsPendingContractExpired(status(c), createdAtUtc(c), utcNow, expiryDays))
            .ToList();
}
