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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
    public ICollection<FulfillmentEvent> Events { get; set; } = new List<FulfillmentEvent>();
}
