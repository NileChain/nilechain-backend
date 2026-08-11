namespace NileChain.Application.Admin;

/// <summary>
/// Soft-path guards for admin role-change hard-deletes of farm/factory.
/// Prefer refusing when contracts exist over cascading deletes.
/// </summary>
public static class EntityDeleteGuards
{
    public static bool CanRemoveFactory(bool hasContracts) => !hasContracts;

    public static bool CanRemoveFarm(bool hasContracts) => !hasContracts;

    public static void EnsureCanRemoveFactory(bool hasContracts)
    {
        if (!CanRemoveFactory(hasContracts))
            throw new InvalidOperationException(
                "Cannot change role: this factory has existing contracts. Cancel or complete contracts first.");
    }

    public static void EnsureCanRemoveFarm(bool hasContracts)
    {
        if (!CanRemoveFarm(hasContracts))
            throw new InvalidOperationException(
                "Cannot change role: this farm has existing contracts. Cancel or complete contracts first.");
    }
}
