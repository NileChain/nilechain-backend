namespace NileChain.AI.RAG;

public class RagPipeline
{
    private readonly ChromaService _chromaService;

    public RagPipeline(ChromaService chromaService)
    {
        _chromaService = chromaService;
    }

    public async Task<string> GetQualityStandardsAsync(string cropType)
    {
        return await _chromaService.QueryAsync(
            $"معايير جودة محصول {cropType} للاستخدام الصناعي",
            nResults: 2);
    }

    public async Task<string> GetContractTemplateAsync(string cropType)
    {
        return await _chromaService.QueryAsync(
            $"قالب عقد توريد {cropType} بنود قانونية",
            nResults: 2);
    }

    public async Task<string> GetCombinedContextAsync(string cropType)
    {
        var qualityContext = await GetQualityStandardsAsync(cropType);
        var contractContext = await GetContractTemplateAsync(cropType);

        return $"""
            معايير الجودة:
            {qualityContext}

            البنود القانونية:
            {contractContext}
            """;
    }
}
