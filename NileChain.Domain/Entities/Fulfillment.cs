using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// Post-signature delivery lifecycle for a fully signed contract (1:1).
/// </summary>
public class Fulfillment
{
    public Guid FulfillmentId { get; set; }
    public Guid ContractId { get; set; }
    public FulfillmentStatus Status { get; set; } = FulfillmentStatus.Planned;

    /// <summary>
    /// Planned ship / delivery calendar date (copied from supply request at creation).
    /// </summary>
    public DateTime? PlannedShipDate { get; set; }

    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? QualityCheckedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public DateTime? VoidedAt { get; set; }

    public string? QualityNotes { get; set; }

    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? ShippedNotes { get; set; }

    /// <summary>Quantity accepted after QC (tons). Null when not recorded.</summary>
    public decimal? AcceptedQuantityTons { get; set; }

    /// <summary>0–100 discount applied to first open payment milestone after QC.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Whether received goods met the supply-request structured quality specs.</summary>
    public bool? SpecsMet { get; set; }

    /// <summary>Notes comparing delivery against requested quality specs.</summary>
    public string? SpecsOutcomeNotes { get; set; }

    /// <summary>Copied from the supply request at full sign. Immutable after create.</summary>
    public DeliveryPoint DeliveryPoint { get; set; } = DeliveryPoint.FactoryGate;
    public DealParty FreightPayer { get; set; } = DealParty.Farm;
    public DealParty TransitRisk { get; set; } = DealParty.Farm;

    /// <summary>Weighbridge tons recorded at receive. Payable tons = min(this, contracted).</summary>
    public decimal? WeighedQuantityTons { get; set; }
    public string? WeighbridgeTicketUrl { get; set; }

    public DateTime? RejectedAtGateAt { get; set; }
    public GateRejectReason? GateRejectReason { get; set; }
    public string? GateRejectNotes { get; set; }
    public DealParty? ReturnFreightBearer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
    public ICollection<FulfillmentEvent> Events { get; set; } = new List<FulfillmentEvent>();
}
