namespace NileChain.Application.Dtos.Factory;

public class FactoryMatchItemDto
{
    public Guid MatchId { get; set; }
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? FarmLocation { get; set; }
    public string? FarmGovernorate { get; set; }
    public decimal? FarmLatitude { get; set; }
    public decimal? FarmLongitude { get; set; }
    public bool FarmIsVerified { get; set; }
    public decimal FarmAverageRating { get; set; }
    public decimal? MatchScore { get; set; }
    public decimal? RiskScore { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
