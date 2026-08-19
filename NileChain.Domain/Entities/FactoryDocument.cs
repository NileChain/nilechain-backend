using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

public class FactoryDocument
{
    public Guid FactoryDocumentId { get; set; }
    public Guid FactoryId { get; set; }
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public long FileSize { get; set; }
    public string FileType { get; set; } = default!;
    public string PublicId { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public KybKind KybKind { get; set; } = KybKind.Other;

    public Factory Factory { get; set; } = default!;
}
