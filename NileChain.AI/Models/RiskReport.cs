namespace NileChain.AI.Models;

public class RiskReport
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public decimal ProfileCompleteness { get; set; }
    public decimal CertificationScore { get; set; }
    public decimal ContractHistoryScore { get; set; }
    public decimal RatingScore { get; set; }
    public string AIAnalysis { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<string> RagSourcesUsed { get; set; } = new();
}
