namespace NileChain.AI.Matching;

/// <summary>
/// Candidate gate: farms whose user is explicitly inactive are excluded from matching.
/// </summary>
public static class ActiveFarmFilter
{
    public static bool IsEligible(bool? userIsActive) => userIsActive != false;

    public static bool IsEligibleUserId(Guid userId, IReadOnlyCollection<Guid> inactiveUserIds) =>
        !inactiveUserIds.Contains(userId);
}
