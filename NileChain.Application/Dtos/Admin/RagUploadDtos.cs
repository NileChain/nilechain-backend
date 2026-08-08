namespace NileChain.Application.Dtos.Admin;

public class RagDocumentDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = "indexed";
}

public class RagUploadResult
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IndexedInChroma { get; set; }
}
