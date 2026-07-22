using NileChain.AI.Agents;
using NileChain.AI.Models;

namespace NileChain.AI.Services;

public class AIOrchestrationService
{
    private readonly OrchestratorAgent _orchestrator;

    public AIOrchestrationService(OrchestratorAgent orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public Task<AgentResponse> ProcessSupplyRequestAsync(AgentRequest request)
    {
        return _orchestrator.RunAsync(request);
    }

    public Task<string> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
        return _orchestrator.GenerateContractAsync(request, selectedFarm, factoryName);
    }
}
