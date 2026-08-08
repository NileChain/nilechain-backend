using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using System.Text;

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
            return ContractGenerationResult.Ok(
                BuildTemplateContract(selectedFarm.FarmName, factoryName, request));
        }

        try
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

            var result = await _kernelProvider.Kernel!.InvokePromptAsync(prompt);
            var text = result.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return ContractGenerationResult.Ok(
                    BuildTemplateContract(selectedFarm.FarmName, factoryName, request));
            }

            return ContractGenerationResult.Ok(text);
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
        sb.AppendLine($"مواصفات الجودة: {request.QualitySpecs}");
        sb.AppendLine();
        sb.AppendLine("شروط الدفع: 30% مقدم، 70% عند الاستلام.");
        sb.AppendLine("فض النزاعات: محاكم القاهرة الاقتصادية.");
        sb.AppendLine();
        sb.AppendLine("توقيع المورد: __________");
        sb.AppendLine("توقيع المشتري: __________");
        return sb.ToString();
    }
}
