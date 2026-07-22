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
        var reports = new List<RiskReport>();

        foreach (var match in matches)
        {
            var report = await _plugin.CalculateRiskScore(match.FarmId.ToString());
            reports.Add(report);
        }

        return reports;
    }
}
