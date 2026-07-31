using NileChain.AI.Models;

namespace NileChain.AI.Agents;

public class OrchestratorAgent
{
    private readonly MatchingAgent _matchingAgent;
    private readonly RiskAgent _riskAgent;
    private readonly ContractAgent _contractAgent;

    public OrchestratorAgent(
        MatchingAgent matchingAgent,
        RiskAgent riskAgent,
        ContractAgent contractAgent)
    {
        _matchingAgent = matchingAgent;
        _riskAgent = riskAgent;
        _contractAgent = contractAgent;
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request)
    {
        try
        {
            // 1) Matching once — MatchingAgent → MatchingPlugin
            var matches = await _matchingAgent.RunAsync(request);

            if (matches.Count == 0)
            {
                return new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "No matching farms found"
                };
            }

            // 2) Risk once per matched farm — RiskAgent → RiskPlugin (no re-matching)
            var riskReports = await _riskAgent.RunAsync(matches);

            // 3) Attach successful RiskReports; keep MatchingPlugin scores if risk failed
            foreach (var match in matches)
            {
                var report = riskReports.FirstOrDefault(r => r.FarmId == match.FarmId);
                if (report is null)
                    continue;

                if (IsFailedRiskReport(report))
                    continue;

                match.RiskScore = report.OverallScore;
                match.RiskLevel = report.RiskLevel;
            }

            // 4) Final ranking with updated RiskScore values
            matches = matches
                .OrderByDescending(m => m.MatchScore)
                .ThenByDescending(m => m.RiskScore)
                .ThenByDescending(m => m.IsVerified)
                .ToList();

            return new AgentResponse
            {
                Success = true,
                TopMatches = matches,
            };
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<string> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
        return _contractAgent.GenerateContractAsync(request, selectedFarm, factoryName);
    }

    private static bool IsFailedRiskReport(RiskReport report) =>
        !string.IsNullOrWhiteSpace(report.AIAnalysis)
        && string.IsNullOrWhiteSpace(report.RiskLevel);
}
