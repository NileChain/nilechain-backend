namespace NileChain.AI.Models;

public class MatchResult
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public decimal RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public List<string> CropTypes { get; set; } = new();

    /// <summary>
    /// Full risk breakdown for this farm (additive; existing score fields unchanged).
    /// </summary>
    public RiskReport? RiskReport { get; set; }
}
