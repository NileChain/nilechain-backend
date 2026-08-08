using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class Farm
{
    public Guid FarmId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public string? Governorate { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? SizeInFeddans { get; set; }
    public SoilType? SoilType { get; set; }
    public decimal? RiskScore { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool IsVerified { get; set; }
    public bool ProfileComplete { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = default!;
    public ICollection<CropType> CropTypes { get; set; } = new List<CropType>();
    public ICollection<FarmDocument> FarmDocuments { get; set; } = new List<FarmDocument>();
    public ICollection<FarmCertification> FarmCertifications { get; set; } = new List<FarmCertification>();
    public ICollection<FarmMatch> FarmMatches { get; set; } = new List<FarmMatch>();
    public ICollection<RiskAssessmentReport> RiskAssessmentReports { get; set; } = new List<RiskAssessmentReport>();
}
