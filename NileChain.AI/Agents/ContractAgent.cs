using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Sbg;
using NileChain.Application.Common;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;
using System.Text;

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
                deliveryPointArabic: PointArabic(request.DeliveryPoint),
                freightPayerArabic: PartyArabic(request.FreightPayer),
                transitRiskArabic: PartyArabic(request.TransitRisk));

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

                    return ContractGenerationResult.Ok(text);
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

    private static string BuildTemplateContract(string farmName, string factoryName, AgentRequest request)
    {
        var total = request.QuantityTons * request.PricePerTon;
        var sb = new StringBuilder();
        sb.AppendLine("بسم الله الرحمن الرحيم");
        sb.AppendLine();
        sb.AppendLine("عقد توريد زراعي (نموذج احتياطي — تم إنشاؤه بدون RAG/LLM)");
        sb.AppendLine();
        sb.AppendLine($"الطرف الأول (المورد): {farmName}");
        sb.AppendLine($"الطرف الثاني (المشتري): {factoryName}");
        sb.AppendLine($"المحصول: {request.CropType}");
        sb.AppendLine($"الكمية: {request.QuantityTons:0.##} طن متري");
        sb.AppendLine($"السعر: {request.PricePerTon:0.##} جنيه/طن");
        sb.AppendLine($"الإجمالي: {total:0.##} جنيه مصري");
        sb.AppendLine($"تاريخ التسليم: {request.DeliveryDate:dd MMMM yyyy}");
        sb.AppendLine($"نقطة التسليم: {PointArabic(request.DeliveryPoint)}");
        sb.AppendLine($"أجرة النقل يتحملها: {PartyArabic(request.FreightPayer)}");
        sb.AppendLine($"مخاطر النقل يتحملها: {PartyArabic(request.TransitRisk)}");
        sb.AppendLine($"مواصفات الجودة: {request.QualitySpecs}");
        sb.AppendLine();
        sb.AppendLine("شروط الدفع: 30% مقدم، 70% عند الاستلام.");
        sb.AppendLine("رفض الحمولة عند بوابة المصنع قبل الاستلام يعيد العربات حسب من يملك النقل ويعيد أي مبلغ محجوز للمشتري.");
        sb.AppendLine("فض النزاعات: محاكم القاهرة الاقتصادية.");
        sb.AppendLine();
        sb.AppendLine("توقيع المورد: __________");
        sb.AppendLine("توقيع المشتري: __________");
        return sb.ToString();
    }

    private static string PointArabic(string? raw)
    {
        DeliveryTermsPolicy.TryParsePoint(raw, out var point);
        if (string.IsNullOrWhiteSpace(raw))
            point = DeliveryPoint.FactoryGate;
        return DeliveryTermsPolicy.ArabicPoint(point);
    }

    private static string PartyArabic(string? raw)
    {
        DeliveryTermsPolicy.TryParseParty(raw, out var party);
        if (string.IsNullOrWhiteSpace(raw))
            party = DealParty.Farm;
        return DeliveryTermsPolicy.ArabicParty(party);
    }
}
