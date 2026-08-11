using NileChain.Application.Common;

namespace NileChain.AI.RAG;

public class RagPipeline
{
    private readonly ChromaService _chromaService;

    public RagPipeline(ChromaService chromaService)
    {
        _chromaService = chromaService;
    }

    public async Task<ChromaLookupResult> GetQualityStandardsAsync(string cropType)
    {
        return await _chromaService.QueryAsync(
            $"معايير جودة محصول {cropType} للاستخدام الصناعي",
            nResults: 2);
    }

    public async Task<ChromaLookupResult> GetContractTemplateAsync(string cropType)
    {
        return await _chromaService.QueryAsync(
            $"قالب عقد توريد {cropType} بنود قانونية",
            nResults: 2);
    }

    public async Task<ChromaLookupResult> GetAgriScienceAsync(string cropType)
    {
        return await _chromaService.QueryAsync(
            $"علوم زراعية وممارسات زراعية لمحصول {cropType}",
            nResults: 2);
    }

    /// <summary>
    /// Combined RAG context. When Chroma is down, <see cref="ChromaLookupResult.IsAvailable"/> is false
    /// and Content is the client-safe "AI service unavailable" message.
    /// </summary>
    public async Task<ChromaLookupResult> GetCombinedContextAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ChromaLookupResult.Empty();

        try
        {
            var qualityTask = GetQualityStandardsAsync(query);
            var contractTask = GetContractTemplateAsync(query);
            var agriScienceTask = GetAgriScienceAsync(query);

            await Task.WhenAll(qualityTask, contractTask, agriScienceTask);

            var qualityResults = await qualityTask;
            var contractResults = await contractTask;
            var agriScienceResults = await agriScienceTask;

            if (!qualityResults.IsAvailable
                || !contractResults.IsAvailable
                || !agriScienceResults.IsAvailable)
            {
                return ChromaLookupResult.Unavailable();
            }

            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(qualityResults.Content))
                sections.Add(FormatSection("QUALITY STANDARDS", qualityResults.Content));

            if (!string.IsNullOrWhiteSpace(contractResults.Content))
                sections.Add(FormatSection("CONTRACT TEMPLATE", contractResults.Content));

            if (!string.IsNullOrWhiteSpace(agriScienceResults.Content))
                sections.Add(FormatSection("AGRI SCIENCE", agriScienceResults.Content));

            if (sections.Count == 0)
                return ChromaLookupResult.Empty();

            return ChromaLookupResult.Ok(
                string.Join(Environment.NewLine + Environment.NewLine, sections));
        }
        catch
        {
            return ChromaLookupResult.Unavailable();
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
