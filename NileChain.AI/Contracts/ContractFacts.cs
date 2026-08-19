using NileChain.AI.Models;

namespace NileChain.AI.Contracts;

/// <summary>
/// The agreed deal, as the platform recorded it. Every number, name, and date that
/// reaches a generated contract is rendered from here — model output never supplies them.
/// </summary>
public sealed record ContractFacts
{
    public string FarmName { get; init; } = string.Empty;
    public string FactoryName { get; init; } = string.Empty;
    public string CropType { get; init; } = string.Empty;
    public decimal QuantityTons { get; init; }
    public decimal PricePerTon { get; init; }
    public DateTime DeliveryDate { get; init; }
    public string? QualitySpecs { get; init; }
    public string DeliveryPointArabic { get; init; } = string.Empty;
    public string FreightPayerArabic { get; init; } = string.Empty;
    public string TransitRiskArabic { get; init; } = string.Empty;

    public decimal TotalValueEgp => QuantityTons * PricePerTon;

    public string QualityArabic =>
        string.IsNullOrWhiteSpace(QualitySpecs)
            ? "وفق المواصفات المتفق عليها بين الطرفين ومعايير القبول لدى المشتري."
            : QualitySpecs.Trim();

    public string DeliveryDateArabic => DeliveryDate.ToString("dd MMMM yyyy");

    public static ContractFacts From(
        string farmName,
        string factoryName,
        string cropType,
        decimal quantityTons,
        decimal pricePerTon,
        DateTime deliveryDate,
        string? qualitySpecs,
        string? deliveryPointRaw,
        string? freightPayerRaw,
        string? transitRiskRaw) =>
        new()
        {
            FarmName = farmName ?? string.Empty,
            FactoryName = factoryName ?? string.Empty,
            CropType = cropType ?? string.Empty,
            QuantityTons = quantityTons,
            PricePerTon = pricePerTon,
            DeliveryDate = deliveryDate,
            QualitySpecs = qualitySpecs,
            DeliveryPointArabic = ContractDraftTemplate.PointArabic(deliveryPointRaw),
            FreightPayerArabic = ContractDraftTemplate.PartyArabic(freightPayerRaw),
            TransitRiskArabic = ContractDraftTemplate.PartyArabic(transitRiskRaw)
        };

    public static ContractFacts From(AgentRequest request, string farmName, string factoryName)
    {
        ArgumentNullException.ThrowIfNull(request);

        return From(
            farmName,
            factoryName,
            request.CropType,
            request.QuantityTons,
            request.PricePerTon,
            // A contract must never carry a zero date; fall back to a month out.
            request.DeliveryDate == default
                ? DateTime.UtcNow.Date.AddDays(30)
                : request.DeliveryDate,
            request.QualitySpecs,
            request.DeliveryPoint,
            request.FreightPayer,
            request.TransitRisk);
    }
}
