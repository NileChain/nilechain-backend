namespace NileChain.Application.Dtos.Contracts;

/// <summary>
/// Structured commercial + legal payload for A4 PDF rendering.
/// Legal clause wording comes from <see cref="GeneratedText"/> (never fabricated here).
/// Template/presentation requires legal/business-owner review before production use.
/// </summary>
public sealed class ContractPdfModel
{
    public Guid ContractId { get; init; }
    public string Title { get; init; } = "Agricultural Supply Agreement";
    public string Status { get; init; } = "Draft";
    public string? DocumentVersion { get; init; } = "1.0";

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? DeliveryDate { get; init; }

    public string FactoryName { get; init; } = string.Empty;
    public string? FactoryLocation { get; init; }
    public string FarmName { get; init; } = string.Empty;
    public string? FarmLocation { get; init; }

    public string CropName { get; init; } = string.Empty;
    public decimal QuantityTons { get; init; }
    public decimal? PricePerTon { get; init; }
    public string? DeliveryLocation { get; init; }
    public string? QualityRequirements { get; init; }
    public string? PaymentTerms { get; init; }
    public decimal? RiskScore { get; init; }

    public string? GeneratedText { get; init; }

    public bool FactorySigned { get; init; }
    public bool FarmSigned { get; init; }
    public DateTime? FactorySignedAt { get; init; }
    public DateTime? FarmSignedAt { get; init; }
}
