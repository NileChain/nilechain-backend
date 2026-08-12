namespace NileChain.Application.Dtos.Farm;

public sealed class FarmPublicProfileDto
{
    public Guid FarmId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Governorate { get; set; }
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? SizeInFeddans { get; set; }
    public string? Description { get; set; }
    public bool IsVerified { get; set; }
    public decimal? RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> CropTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Certifications { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();
    public IReadOnlyList<FarmPublicDocumentDto> Documents { get; set; } =
        Array.Empty<FarmPublicDocumentDto>();
    public FarmPublicRatingSummaryDto Rating { get; set; } = new();
}

public sealed class FarmPublicDocumentDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public sealed class FarmPublicRatingSummaryDto
{
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
}
