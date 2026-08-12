namespace NileChain.Domain.Entities;

/// <summary>Public gallery photo for farm marketplace profile.</summary>
public class FarmImage
{
    public Guid FarmImageId { get; set; }
    public Guid FarmId { get; set; }
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public string PublicId { get; set; } = default!;
    public int SortOrder { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Farm Farm { get; set; } = default!;
}
