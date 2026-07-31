using NileChain.AI.Models;
using NileChain.AI.Plugins;

namespace NileChain.AI.Agents;

public class RiskAgent
{
    private readonly RiskPlugin _plugin;

    public RiskAgent(RiskPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task<List<RiskReport>> RunAsync(List<MatchResult> matches)
    {
        var reports = new List<RiskReport>(matches.Count);

        // Sequential: RiskPlugin uses a scoped DbContext (not thread-safe for parallel calls).
        foreach (var match in matches)
        {
            try
            {
                var report = await _plugin.CalculateRiskScore(match.FarmId);
                reports.Add(report);
            }
            catch (Exception ex)
            {
                reports.Add(new RiskReport
                {
                    FarmId = match.FarmId,
                    FarmName = match.FarmName,
                    AIAnalysis = $"Risk calculation failed: {ex.Message}"
                });
            }
        }

        return reports;
    }
}
