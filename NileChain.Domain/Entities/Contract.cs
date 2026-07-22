using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

public class Contract
{
    public Guid ContractId { get; set; }
    public Guid MatchId { get; set; }
    public string? GeneratedText { get; set; }
    public string? PdfUrl { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }

    public FarmMatch FarmMatch { get; set; } = default!;
    public ICollection<RagDocument> RagDocuments { get; set; } = new List<RagDocument>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
