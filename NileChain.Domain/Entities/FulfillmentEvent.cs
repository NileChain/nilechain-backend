using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

/// <summary>Append-only audit row for a fulfillment status change.</summary>
public class FulfillmentEvent
{
    public Guid EventId { get; set; }
    public Guid FulfillmentId { get; set; }
    public FulfillmentStatus? FromStatus { get; set; }
    public FulfillmentStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Fulfillment Fulfillment { get; set; } = default!;
    public ApplicationUser ActorUser { get; set; } = default!;
}
