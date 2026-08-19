using NileChain.Application.Common;

namespace NileChain.AI.RAG;

public class RagPipeline
{
    public const string SectionQuality = "معايير الجودة";
    public const string SectionContract = "قوالب العقود";
    public const string SectionAgriScience = "علوم زراعية";

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
    /// Combined retrieval across the three knowledge sections, numbered for citation.
    /// When Chroma is down the result is <see cref="RagContext.Unavailable"/>; when it answers
    /// with nothing the result is <see cref="RagContext.Empty"/> — callers must treat those
    /// differently from a real hit, since neither one grounds an answer.
    /// </summary>
    public async Task<RagContext> GetCombinedContextAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return RagContext.Empty();

        try
        {
            var qualityTask = GetQualityStandardsAsync(query);
            var contractTask = GetContractTemplateAsync(query);
            var agriScienceTask = GetAgriScienceAsync(query);

            await Task.WhenAll(qualityTask, contractTask, agriScienceTask);

            var quality = await qualityTask;
            var contract = await contractTask;
            var agriScience = await agriScienceTask;

            if (!quality.IsAvailable || !contract.IsAvailable || !agriScience.IsAvailable)
                return RagContext.Unavailable(ClientErrorSanitizer.ServiceUnavailableMessage);

            var passages = new List<(string Section, RagChunk Chunk)>();
            Collect(passages, SectionQuality, quality);
            Collect(passages, SectionContract, contract);
            Collect(passages, SectionAgriScience, agriScience);

            return RagContext.FromSections(passages);
        }
        catch
        {
            return RagContext.Unavailable(ClientErrorSanitizer.ServiceUnavailableMessage);
        }
    }

    private static void Collect(
        List<(string Section, RagChunk Chunk)> passages,
        string section,
        ChromaLookupResult lookup)
    {
        foreach (var chunk in lookup.Chunks)
            passages.Add((section, chunk));
    }
}
