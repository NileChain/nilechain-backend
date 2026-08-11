using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class DisputeEvent
{
    public Guid EventId { get; set; }
    public Guid DisputeId { get; set; }
    public DisputeStatus? FromStatus { get; set; }
    public DisputeStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Dispute Dispute { get; set; } = default!;
    public ApplicationUser ActorUser { get; set; } = default!;
}
