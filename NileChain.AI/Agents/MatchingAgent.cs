using NileChain.AI.Models;
using NileChain.AI.Plugins;

namespace NileChain.AI.Agents;

public class MatchingAgent
{
    private readonly MatchingPlugin _plugin;

    public MatchingAgent(MatchingPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task<List<MatchResult>> RunAsync(AgentRequest request)
    {
        return await _plugin.FindMatchingFarms(
            request.CropType,
            request.QuantityTons,
            request.FactoryGovernorate);
    }
}
