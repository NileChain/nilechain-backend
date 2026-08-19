using NileChain.AI.Models;

namespace NileChain.AI.Contracts;

/// <summary>
/// Last line of defence before a draft is stored: the text must still carry the deal the
/// platform agreed to. Presence of the right words is not enough — the figures must match,
/// so an altered quantity or price is caught rather than shipped.
/// </summary>
public static class ContractTermsGuard
{
    public static bool Validate(
        string? contractText,
        AgentRequest request,
        string farmName,
        string factoryName,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        error = null;
        if (string.IsNullOrWhiteSpace(contractText))
        {
            error = "Contract text is empty.";
            return false;
        }

        var facts = ContractFacts.From(request, farmName, factoryName);
        var missing = new List<string>();

        // Party names must appear as exact strings, not just the generic «المشتري» / «المورد» labels.
        if (!Contains(contractText, farmName))
            missing.Add("parties/farm");
        if (!Contains(contractText, factoryName))
            missing.Add("parties/factory");
        if (!ContainsAny(contractText, request.CropType, "المحصول"))
            missing.Add("crop");

        if (!Contains(contractText, facts.QuantityTons.ToString("0.##")))
            missing.Add("quantity");
        if (!Contains(contractText, facts.PricePerTon.ToString("0.##")))
            missing.Add("price");
        if (!Contains(contractText, facts.TotalValueEgp.ToString("0.##")))
            missing.Add("total value");
        if (!Contains(contractText, facts.DeliveryDateArabic))
            missing.Add("delivery date");

        if (missing.Count == 0)
            return true;

        error = "Missing or altered agreed terms: " + string.Join(", ", missing);
        return false;
    }

    private static bool Contains(string haystack, string? needle) =>
        !string.IsNullOrWhiteSpace(needle)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string haystack, params string?[] needles) =>
        needles.Any(n => Contains(haystack, n));
}
