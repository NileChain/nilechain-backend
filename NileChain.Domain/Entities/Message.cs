using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class Message
{
    public Guid MessageId { get; set; }
    public Guid MatchId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public FarmMatch FarmMatch { get; set; } = default!;
    public ApplicationUser Sender { get; set; } = default!;
    public ApplicationUser Receiver { get; set; } = default!;
}
