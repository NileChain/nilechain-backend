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

    public async Task<string> GetCombinedContextAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        try
        {
            var qualityTask = _chromaService.QueryAsync(
                $"معايير جودة محصول {query} للاستخدام الصناعي",
                nResults: 2);

            var contractTask = _chromaService.QueryAsync(
                $"قالب عقد توريد {query} بنود قانونية",
                nResults: 2);

            await Task.WhenAll(qualityTask, contractTask);

            var qualityResults = await qualityTask;
            var contractResults = await contractTask;

            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(qualityResults))
            {
                sections.Add(
                    "=== QUALITY STANDARDS ==="
                    + Environment.NewLine
                    + Environment.NewLine
                    + qualityResults.Trim());
            }

            if (!string.IsNullOrWhiteSpace(contractResults))
            {
                sections.Add(
                    "=== CONTRACT TEMPLATE ==="
                    + Environment.NewLine
                    + Environment.NewLine
                    + contractResults.Trim());
            }

            if (sections.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine + Environment.NewLine, sections);
        }
        catch
        {
            return string.Empty;
        }
    }
}
