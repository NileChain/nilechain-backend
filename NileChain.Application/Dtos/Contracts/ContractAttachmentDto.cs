namespace NileChain.Application.Dtos.Contracts;

public sealed class ContractAttachmentDto
{
    public Guid AttachmentId { get; set; }
    public Guid ContractId { get; set; }
    public string Kind { get; set; } = "Other";
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
