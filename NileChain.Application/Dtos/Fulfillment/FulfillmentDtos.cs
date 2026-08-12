using NileChain.Domain.Enums;

namespace NileChain.Application.Dtos.Fulfillment;

public sealed class FulfillmentDto
{
    public Guid FulfillmentId { get; init; }
    public Guid ContractId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime? PlannedShipDate { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public DateTime? QualityCheckedAt { get; init; }
    public DateTime? FulfilledAt { get; init; }
    public DateTime? VoidedAt { get; init; }
    public string? QualityNotes { get; init; }
    public string? Carrier { get; init; }
    public string? TrackingNumber { get; init; }
    public string? ShippedNotes { get; init; }
    public decimal? AcceptedQuantityTons { get; init; }
    public decimal DiscountPercent { get; init; }
    public bool? SpecsMet { get; init; }
    public string? SpecsOutcomeNotes { get; init; }
    public string DeliveryPoint { get; init; } = "FactoryGate";
    public string FreightPayer { get; init; } = "Farm";
    public string TransitRisk { get; init; } = "Farm";
    public decimal? ContractedQuantityTons { get; init; }
    public decimal? WeighedQuantityTons { get; init; }
    public string? WeighbridgeTicketUrl { get; init; }
    public DateTime? RejectedAtGateAt { get; init; }
    public string? GateRejectReason { get; init; }
    public string? GateRejectNotes { get; init; }
    public string? ReturnFreightBearer { get; init; }
    public NileChain.Application.Dtos.Factory.StructuredQualitySpecsDto? RequestedQuality { get; init; }
    public IReadOnlyList<FulfillmentEventDto> Events { get; init; } = Array.Empty<FulfillmentEventDto>();
}

public sealed class FulfillmentEventDto
{
    public Guid EventId { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = default!;
    public Guid ActorUserId { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class StuckFulfillmentDto
{
    public Guid FulfillmentId { get; init; }
    public Guid ContractId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime? PlannedShipDate { get; init; }
    public string? FarmName { get; init; }
    public string? FactoryName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class StuckFulfillmentListDto
{
    public IReadOnlyList<StuckFulfillmentDto> Items { get; init; } = Array.Empty<StuckFulfillmentDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class ShipFulfillmentRequest
{
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReceiveFulfillmentRequest
{
    /// <summary>Weighbridge tons. Required and must be &gt; 0.</summary>
    public decimal WeighedQuantityTons { get; set; }
    public string? WeighbridgeTicketUrl { get; set; }
}

public sealed class RejectAtGateRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class QualityCheckRequest
{
    public string? Notes { get; set; }
    public decimal? AcceptedQuantityTons { get; set; }
    /// <summary>0–100. Applied to the first open (Pending/MarkedPaid) milestone amount.</summary>
    public decimal DiscountPercent { get; set; }
    /// <summary>Whether delivery met the supply-request structured quality specs.</summary>
    public bool? SpecsMet { get; set; }
    /// <summary>Notes comparing delivery against requested quality specs.</summary>
    public string? SpecsOutcomeNotes { get; set; }
}
