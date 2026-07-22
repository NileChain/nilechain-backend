using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;

namespace NileChain.AI.Agents;

public class ContractAgent
{
    private readonly Kernel _kernel;
    private readonly ContractPlugin _plugin;
    private readonly RagPipeline _ragPipeline;

    public ContractAgent(
        Kernel kernel,
        ContractPlugin plugin,
        RagPipeline ragPipeline)
    {
        _kernel = kernel;
        _plugin = plugin;
        _ragPipeline = ragPipeline;
    }

    public async Task<string> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
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

        var result = await _kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }
}
