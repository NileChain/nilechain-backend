using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NileChain.AI.Contracts;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Sbg;
using NileChain.AI.Telemetry;
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
    private readonly LlmUsageLedger _usage;
    private readonly ILogger<ContractAgent> _logger;

    public ContractAgent(
        OpenAiKernelProvider kernelProvider,
        ContractPlugin plugin,
        RagPipeline ragPipeline,
        IConfiguration configuration,
        SbgStudentChatClient sbgClient,
        LlmUsageLedger usage,
        ILogger<ContractAgent> logger)
    {
        _kernelProvider = kernelProvider;
        _plugin = plugin;
        _ragPipeline = ragPipeline;
        _configuration = configuration;
        _sbgClient = sbgClient;
        _usage = usage;
        _logger = logger;
    }

    public async Task<ContractGenerationResult> GenerateContractAsync(
        AgentRequest request,
        MatchResult selectedFarm,
        string factoryName)
    {
        request ??= new AgentRequest();
        selectedFarm ??= new MatchResult();
        factoryName ??= string.Empty;

        var facts = ContractFacts.From(request, selectedFarm.FarmName, factoryName);

        var chain = LlmKernelFactory.ResolveProviderChain(_configuration);
        if (chain.Count == 0 && !_kernelProvider.IsAvailable)
            return ContractGenerationResult.Ok(ContractComposer.Compose(facts));

        try
        {
            var ragLookup = await _ragPipeline.GetCombinedContextAsync(request.CropType);
            // Plain text, not the cited rendering: bracketed markers are rejected by the clause guard.
            var ragContext = ragLookup.HasKnowledge
                ? ragLookup.PlainText
                : "(لا يوجد مرجع في قاعدة المعرفة لهذا المحصول)";

            var prompt = _plugin.BuildStructuredClausePrompt(
                farmName: facts.FarmName,
                factoryName: facts.FactoryName,
                cropType: facts.CropType,
                quantityTons: facts.QuantityTons,
                pricePerTon: facts.PricePerTon,
                deliveryDate: facts.DeliveryDateArabic,
                qualitySpecs: request.QualitySpecs,
                ragContext: ragContext,
                deliveryPointArabic: facts.DeliveryPointArabic,
                freightPayerArabic: facts.FreightPayerArabic,
                transitRiskArabic: facts.TransitRiskArabic);

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
                    _sbgClient,
                    _usage);
                if (kernel is null)
                    continue;

                try
                {
                    var result = await kernel.InvokePromptAsync(prompt);
                    if (!ContractClauseDraft.TryParse(result.ToString(), out var clauses, out var reason))
                    {
                        _logger.LogInformation(
                            "Contract clause draft from {Provider} unusable ({Reason}); trying next provider",
                            providerKey,
                            reason);
                        continue;
                    }

                    if (reason is not null)
                    {
                        _logger.LogInformation(
                            "Contract clause draft from {Provider} partially accepted: {Reason}",
                            providerKey,
                            reason);
                    }

                    // Numbers, names, and dates come from facts either way — the model only styled the prose.
                    return ContractGenerationResult.Ok(ContractComposer.Compose(facts, clauses));
                }
                catch (Exception ex) when (LlmKernelFactory.IsProviderFailure(ex))
                {
                    _logger.LogWarning(ex, "Contract provider {Provider} failed", providerKey);
                }
            }

            return ContractGenerationResult.Ok(ContractComposer.Compose(facts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contract generation fell back to the deterministic template");
            return ContractGenerationResult.Ok(ContractComposer.Compose(facts));
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
                    _sbgClient,
                    _usage);
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
}



