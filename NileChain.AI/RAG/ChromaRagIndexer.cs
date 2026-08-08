using NileChain.Domain.Interfaces;

namespace NileChain.AI.RAG;

public sealed class ChromaRagIndexer : IRagIndexer
{
    private readonly ChromaService _chromaService;

    public ChromaRagIndexer(ChromaService chromaService)
    {
        _chromaService = chromaService;
    }

    public async Task<bool> IndexDocumentAsync(
        string documentId,
        string title,
        string? category,
        string content,
        CancellationToken cancellationToken = default)
    {
        var upserted = await _chromaService.UpsertDocumentsAsync(
            "nilechain_knowledge",
            [
                new ChromaSeedDocument(
                    documentId,
                    $"{title}\n\n{content}",
                    new Dictionary<string, object>
                    {
                        ["title"] = title,
                        ["category"] = category ?? "general"
                    })
            ],
            cancellationToken);

        return upserted > 0;
    }
}
