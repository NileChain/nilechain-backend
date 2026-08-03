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

    public async Task<string> GetAgriScienceAsync(string cropType)
    {
        return await _chromaService.QueryAsync(
            $"علوم زراعية وممارسات زراعية لمحصول {cropType}",
            nResults: 2);
    }

    public async Task<string> GetCombinedContextAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        try
        {
            var qualityTask = GetQualityStandardsAsync(query);
            var contractTask = GetContractTemplateAsync(query);
            var agriScienceTask = GetAgriScienceAsync(query);

            await Task.WhenAll(qualityTask, contractTask, agriScienceTask);

            var qualityResults = await qualityTask;
            var contractResults = await contractTask;
            var agriScienceResults = await agriScienceTask;

            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(qualityResults))
            {
                sections.Add(FormatSection("QUALITY STANDARDS", qualityResults));
            }

            if (!string.IsNullOrWhiteSpace(contractResults))
            {
                sections.Add(FormatSection("CONTRACT TEMPLATE", contractResults));
            }

            if (!string.IsNullOrWhiteSpace(agriScienceResults))
            {
                sections.Add(FormatSection("AGRI SCIENCE", agriScienceResults));
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

    private static string FormatSection(string title, string body) =>
        "=================================="
        + Environment.NewLine
        + title
        + Environment.NewLine
        + "=================================="
        + Environment.NewLine
        + Environment.NewLine
        + body.Trim();
}
