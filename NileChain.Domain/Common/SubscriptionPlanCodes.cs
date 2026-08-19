namespace NileChain.Domain.Common;

public static class SubscriptionPlanCodes
{
    public const string FactoryFree = "factory.free";
    public const string FactoryPro = "factory.pro";
    public const string FarmFree = "farm.free";
    public const string FarmPro = "farm.pro";

    public static bool IsFactory(string? planCode) =>
        StartsWithRole(planCode, "factory.");

    public static bool IsFarm(string? planCode) =>
        StartsWithRole(planCode, "farm.");

    public static bool IsPro(string? planCode) =>
        string.Equals(planCode, FactoryPro, StringComparison.OrdinalIgnoreCase)
        || string.Equals(planCode, FarmPro, StringComparison.OrdinalIgnoreCase);

    public static bool IsFree(string? planCode) =>
        string.Equals(planCode, FactoryFree, StringComparison.OrdinalIgnoreCase)
        || string.Equals(planCode, FarmFree, StringComparison.OrdinalIgnoreCase);

    public static string FreeForRole(bool asFarm) => asFarm ? FarmFree : FactoryFree;

    public static string ProForRole(bool asFarm) => asFarm ? FarmPro : FactoryPro;

    public static bool TryNormalize(string? planCode, out string normalized)
    {
        normalized = (planCode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is FactoryFree or FactoryPro or FarmFree or FarmPro;
    }

    private static bool StartsWithRole(string? planCode, string prefix) =>
        planCode is not null
        && planCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
