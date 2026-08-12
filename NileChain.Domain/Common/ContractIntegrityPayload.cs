namespace NileChain.Domain.Common;

/// <summary>
/// Canonical fields hashed into a NileChain integrity anchor.
/// </summary>
public sealed record ContractIntegrityPayload(
    Guid ContractId,
    string FarmName,
    string FactoryName,
    string CropName,
    decimal QuantityTons,
    decimal PricePerTon,
    DateTime? DeliveryDate,
    string GeneratedText,
    DateTime FarmSignedAt,
    DateTime FactorySignedAt);
