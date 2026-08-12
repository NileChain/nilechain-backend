namespace NileChain.Application.Dtos.Farm;

public class FarmImageDto
{
    public Guid ImageId { get; set; }
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public int SortOrder { get; set; }
}
