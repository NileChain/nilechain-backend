using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class RagDocument
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = default!;
    public string? Category { get; set; }
    public string FilePath { get; set; } = default!;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser Uploader { get; set; } = default!;
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
