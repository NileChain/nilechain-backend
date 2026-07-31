using NileChain.AI.Agents;
using NileChain.AI.Models;

namespace NileChain.AI.Services;

public class AIOrchestrationService
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly Lazy<ContractAgent> _contractAgent;

    public AIOrchestrationService(
        OrchestratorAgent orchestrator,
        Lazy<ContractAgent> contractAgent)
    {
        _orchestrator = orchestrator;
        _contractAgent = contractAgent;
    }

    public Task<AgentResponse> ProcessSupplyRequestAsync(AgentRequest request)
    {
        return _orchestrator.RunAsync(request);
    }

    public Task<ContractGenerationResult> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
        // Resolves ContractAgent only for contract generation (matching remains OpenAI-free).
        return _contractAgent.Value.GenerateContractAsync(request, selectedFarm, factoryName);
    }
}
