using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// Real evidence file attached to a contract (Cloudinary-backed; same shape as farm docs).
/// </summary>
public class ContractAttachment
{
    public Guid AttachmentId { get; set; }
    public Guid ContractId { get; set; }
    public ContractAttachmentKind Kind { get; set; } = ContractAttachmentKind.Other;
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long FileSize { get; set; }
    public string PublicId { get; set; } = default!;
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
}
