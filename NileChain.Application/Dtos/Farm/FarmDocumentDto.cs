namespace NileChain.Application.Dtos.Farm;

public class FarmDocumentDto
{
    public Guid DocumentId { get; set; }
    public string Name { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public string Size { get; set; } = default!;
    public string FileType { get; set; } = default!;
}
