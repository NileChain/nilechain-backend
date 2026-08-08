namespace NileChain.Domain.Interfaces;

/// <summary>
/// Optional RAG indexer (Chroma). Soft-fails when the vector store is unavailable.
/// </summary>
public interface IRagIndexer
{
    Task<bool> IndexDocumentAsync(
        string documentId,
        string title,
        string? category,
        string content,
        CancellationToken cancellationToken = default);
}
