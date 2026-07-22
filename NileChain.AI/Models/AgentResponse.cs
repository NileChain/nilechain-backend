namespace NileChain.AI.Models;

public class AgentResponse
{
    public bool Success { get; set; }
    public List<MatchResult> TopMatches { get; set; } = new();
    public string ComparisonReport { get; set; } = string.Empty;
    public string ContractDraft { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
