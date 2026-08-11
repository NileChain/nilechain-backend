namespace NileChain.Domain.Entities;

/// <summary>
/// Evidence attachment for a dispute — same Cloudinary shape as farm documents.
/// </summary>
public class DisputeEvidence
{
    public Guid DisputeEvidenceId { get; set; }
    public Guid DisputeId { get; set; }
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public long FileSize { get; set; }
    public string FileType { get; set; } = default!;
    public string PublicId { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Dispute Dispute { get; set; } = default!;
}
