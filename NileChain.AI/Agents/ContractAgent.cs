using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Sbg;
using NileChain.Application.Common;
using NileChain.Domain.Common;

namespace NileChain.AI.Agents;

public class ContractAgent
{
    private readonly OpenAiKernelProvider _kernelProvider;
    private readonly ContractPlugin _plugin;
    private readonly RagPipeline _ragPipeline;
    private readonly IConfiguration _configuration;
    private readonly SbgStudentChatClient _sbgClient;

    public ContractAgent(
        OpenAiKernelProvider kernelProvider,
        ContractPlugin plugin,
        RagPipeline ragPipeline,
        IConfiguration configuration,
        SbgStudentChatClient sbgClient)
    {
        _kernelProvider = kernelProvider;
        _plugin = plugin;
        _ragPipeline = ragPipeline;
        _configuration = configuration;
        _sbgClient = sbgClient;
    }

    public async Task<ContractGenerationResult> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
        request ??= new AgentRequest();
        selectedFarm ??= new MatchResult();
        factoryName ??= string.Empty;

        var chain = LlmKernelFactory.ResolveProviderChain(_configuration);
        if (chain.Count == 0 && !_kernelProvider.IsAvailable)
        {
            return ContractGenerationResult.Ok(
                BuildTemplateContract(selectedFarm.FarmName, factoryName, request));
        }

        try
        {
            var ragLookup = await _ragPipeline.GetCombinedContextAsync(request.CropType);
            var ragContext = ragLookup.IsAvailable
                ? ragLookup.Content
                : $"[{ClientErrorSanitizer.ServiceUnavailableMessage}]";

            var prompt = _plugin.BuildContractPrompt(
                farmName: selectedFarm.FarmName,
                factoryName: factoryName,
                cropType: request.CropType,
                quantityTons: request.QuantityTons,
                pricePerTon: request.PricePerTon,
                deliveryDate: request.DeliveryDate.ToString("dd MMMM yyyy"),
                qualitySpecs: request.QualitySpecs,
                ragContext: ragContext,
                deliveryPointArabic: ContractDraftTemplate.PointArabic(request.DeliveryPoint),
                freightPayerArabic: ContractDraftTemplate.PartyArabic(request.FreightPayer),
                transitRiskArabic: ContractDraftTemplate.PartyArabic(request.TransitRisk));

            Exception? lastFailure = null;
            foreach (var providerKey in chain.Count > 0
                         ? chain
                         : new[] { LlmKernelFactory.ProviderOpenAi })
            {
                var kernel = LlmKernelFactory.CreateKernelForProvider(
                    providerKey,
                    _configuration,
                    out _,
                    out _,
                    out _,
                    _sbgClient);
                if (kernel is null)
                    continue;

                try
                {
                    var result = await kernel.InvokePromptAsync(prompt);
                    var text = result.ToString();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    return ContractGenerationResult.Ok(
                        ContractSignatureText.StripHandwrittenBlocks(text));
                }
                catch (Exception ex) when (LlmKernelFactory.IsProviderFailure(ex))
                {
                    lastFailure = ex;
                }
            }

            if (lastFailure is not null)
            {
                return ContractGenerationResult.Ok(
                    BuildTemplateContract(selectedFarm.FarmName, factoryName, request));
            }

            return ContractGenerationResult.Ok(
                BuildTemplateContract(selectedFarm.FarmName, factoryName, request));
        }
        catch (Exception)
        {
            return ContractGenerationResult.Ok(
                BuildTemplateContract(selectedFarm.FarmName, factoryName, request));
        }
    }

    public async Task<ContractGenerationResult> ReviseContractAsync(
        string currentContractText,
        string changeInstructions)
    {
        if (string.IsNullOrWhiteSpace(currentContractText))
            return ContractGenerationResult.Unavailable("No contract text to revise.");
        if (string.IsNullOrWhiteSpace(changeInstructions))
            return ContractGenerationResult.Unavailable("Change instructions are required.");

        var chain = LlmKernelFactory.ResolveProviderChain(_configuration);
        if (chain.Count == 0 && !_kernelProvider.IsAvailable)
        {
            return ContractGenerationResult.Ok(
                BuildInstructionAppendix(currentContractText, changeInstructions));
        }

        try
        {
            var prompt = _plugin.BuildRevisionPrompt(
                currentContractText.Trim(),
                changeInstructions.Trim());

            Exception? lastFailure = null;
            foreach (var providerKey in chain.Count > 0
                         ? chain
                         : new[] { LlmKernelFactory.ProviderOpenAi })
            {
                var kernel = LlmKernelFactory.CreateKernelForProvider(
                    providerKey,
                    _configuration,
                    out _,
                    out _,
                    out _,
                    _sbgClient);
                if (kernel is null)
                    continue;

                try
                {
                    var result = await kernel.InvokePromptAsync(prompt);
                    var text = result.ToString();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    return ContractGenerationResult.Ok(
                        ContractSignatureText.StripHandwrittenBlocks(text));
                }
                catch (Exception ex) when (LlmKernelFactory.IsProviderFailure(ex))
                {
                    lastFailure = ex;
                }
            }

            _ = lastFailure;
            return ContractGenerationResult.Ok(
                BuildInstructionAppendix(currentContractText, changeInstructions));
        }
        catch (Exception)
        {
            return ContractGenerationResult.Ok(
                BuildInstructionAppendix(currentContractText, changeInstructions));
        }
    }

    private static string BuildInstructionAppendix(string currentText, string instructions)
    {
        var cleaned = ContractSignatureText.StripHandwrittenBlocks(currentText).TrimEnd();
        return $"""
            {cleaned}

            المادة — ملحق تعديلات متفق على صياغتها عبر منصة NileChain
            بناءً على طلب أحد الطرفين قبل التوقيع النهائي، تُعدَّل أحكام العقد وفق التعليمات التالية، وتسود على ما يخالفها في المواد السابقة بقدر التعارض فقط:
            {instructions.Trim()}
            ويبقى ما عدا ذلك من أحكام العقد سارياً دون تغيير.
            """;
    }

    private static string BuildTemplateContract(string farmName, string factoryName, AgentRequest request)
    {
        return ContractDraftTemplate.Build(
            farmName ?? string.Empty,
            factoryName ?? string.Empty,
            request.CropType ?? string.Empty,
            request.QuantityTons,
            request.PricePerTon,
            request.DeliveryDate == default
                ? DateTime.UtcNow.Date.AddDays(30)
                : request.DeliveryDate,
            request.QualitySpecs,
            request.DeliveryPoint,
            request.FreightPayer,
            request.TransitRisk);
    }
}
