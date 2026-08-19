using NileChain.AI.Contracts;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.AI;

/// <summary>
/// Deterministic Arabic supply-contract draft — the zero-LLM case of
/// <see cref="ContractComposer"/>, used when the model or RAG is unavailable.
/// </summary>
public static class ContractDraftTemplate
{
    public static string Build(
        string farmName,
        string factoryName,
        string cropType,
        decimal quantityTons,
        decimal pricePerTon,
        DateTime deliveryDate,
        string? qualitySpecs,
        string? deliveryPointRaw = null,
        string? freightPayerRaw = null,
        string? transitRiskRaw = null) =>
        ContractComposer.Compose(
            ContractFacts.From(
                farmName,
                factoryName,
                cropType,
                quantityTons,
                pricePerTon,
                deliveryDate,
                qualitySpecs,
                deliveryPointRaw,
                freightPayerRaw,
                transitRiskRaw));

    public static string PointArabic(string? raw)
    {
        DeliveryTermsPolicy.TryParsePoint(raw, out var point);
        if (string.IsNullOrWhiteSpace(raw))
            point = DeliveryPoint.FactoryGate;
        return DeliveryTermsPolicy.ArabicPoint(point);
    }

    public static string PartyArabic(string? raw)
    {
        DeliveryTermsPolicy.TryParseParty(raw, out var party);
        if (string.IsNullOrWhiteSpace(raw))
            party = DealParty.Farm;
        return DeliveryTermsPolicy.ArabicParty(party);
    }
}
