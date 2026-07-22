namespace NileChain.Domain.Entities;

public class FarmDocument
{
    public Guid FarmDocumentId { get; set; }
    public Guid FarmId { get; set; }
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public long FileSize { get; set; }
    public string FileType { get; set; } = default!;
    public string PublicId { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Farm Farm { get; set; } = default!;
}
