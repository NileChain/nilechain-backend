using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;

namespace NileChain.AI.Agents;

public class ContractAgent
{
    private readonly OpenAiKernelProvider _kernelProvider;
    private readonly ContractPlugin _plugin;
    private readonly RagPipeline _ragPipeline;

    public ContractAgent(
        OpenAiKernelProvider kernelProvider,
        ContractPlugin plugin,
        RagPipeline ragPipeline)
    {
        _kernelProvider = kernelProvider;
        _plugin = plugin;
        _ragPipeline = ragPipeline;
    }

    public async Task<ContractGenerationResult> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
        if (!_kernelProvider.IsAvailable)
        {
            return ContractGenerationResult.Unavailable(
                _kernelProvider.UnavailableReason
                ?? "AI service is unavailable. OpenAI is not configured.");
        }

        var ragContext = await _ragPipeline.GetCombinedContextAsync(request.CropType);

        var prompt = _plugin.BuildContractPrompt(
            farmName: selectedFarm.FarmName,
            factoryName: factoryName,
            cropType: request.CropType,
            quantityTons: request.QuantityTons,
            pricePerTon: request.PricePerTon,
            deliveryDate: request.DeliveryDate.ToString("dd MMMM yyyy"),
            qualitySpecs: request.QualitySpecs,
            ragContext: ragContext);

        var result = await _kernelProvider.Kernel!.InvokePromptAsync(prompt);
        return ContractGenerationResult.Ok(result.ToString());
    }
}
