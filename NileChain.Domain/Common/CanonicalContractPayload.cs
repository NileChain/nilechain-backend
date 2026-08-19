namespace NileChain.Domain.Common;

/// <summary>Content-only fields hashed at signing time (no signature timestamps).</summary>
public sealed record CanonicalContractPayload(
    Guid ContractId,
    Guid MatchId,
    string FarmName,
    string FactoryName,
    string CropName,
    decimal QuantityTons,
    decimal PricePerTon,
    DateTime? DeliveryDate,
    string? GeneratedText);
