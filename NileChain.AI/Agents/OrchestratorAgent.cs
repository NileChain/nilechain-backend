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
            var matches = await _matchingAgent.RunAsync(request);

            if (matches.Count == 0)
            {
                return new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "No matching farms found"
                };
            }

            var riskReports = await _riskAgent.RunAsync(matches);

            foreach (var match in matches)
            {
                var report = riskReports.FirstOrDefault(r => r.FarmId == match.FarmId);
                if (report is not null)
                {
                    match.RiskScore = report.OverallScore;
                    match.RiskLevel = report.RiskLevel;
                }
            }

            matches = matches
                .OrderByDescending(m => (m.MatchScore * 0.6m) + (m.RiskScore * 0.4m))
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
}
