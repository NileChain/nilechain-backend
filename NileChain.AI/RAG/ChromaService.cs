using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NileChain.Application.Common;

namespace NileChain.AI.RAG;

public class ChromaService
{
    private readonly HttpClient _httpClient;
    private const string CollectionName = "nilechain_knowledge";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChromaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Queries Chroma. Distinguishes empty results from service unavailability.
    /// </summary>
    public async Task<ChromaLookupResult> QueryAsync(string query, int nResults = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ChromaLookupResult.Empty();

        try
        {
            var request = new ChromaQueryRequest
            {
                QueryTexts = [query],
                NResults = nResults,
                CollectionName = CollectionName
            };

            var response = await _httpClient.PostAsJsonAsync("/query", request, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return ChromaLookupResult.Unavailable();

            var result = await response.Content
                .ReadFromJsonAsync<ChromaHttpQueryResult>(JsonOptions);

            if (result?.Documents is null || result.Documents.Count == 0)
                return ChromaLookupResult.Empty();

            var chunks = ReadChunks(result);

            return chunks.Count == 0
                ? ChromaLookupResult.Empty()
                : ChromaLookupResult.Ok(chunks);
        }
        catch (HttpRequestException)
        {
            return ChromaLookupResult.Unavailable();
        }
        catch (TaskCanceledException)
        {
            return ChromaLookupResult.Unavailable();
        }
        catch (JsonException)
        {
            return ChromaLookupResult.Unavailable();
        }
        catch (Exception)
        {
            return ChromaLookupResult.Unavailable();
        }
    }

    /// <summary>
    /// Flattens Chroma's per-query batches, pairing each document with the id and title that
    /// make it citable. Ids and metadata are optional in the proxy response, so both degrade
    /// to a positional id and an "unnamed source" title rather than dropping the passage.
    /// </summary>
    private static List<RagChunk> ReadChunks(ChromaHttpQueryResult result)
    {
        var chunks = new List<RagChunk>();

        for (var batch = 0; batch < result.Documents.Count; batch++)
        {
            var documents = result.Documents[batch];
            if (documents is null)
                continue;

            for (var i = 0; i < documents.Count; i++)
            {
                var document = documents[i];
                if (string.IsNullOrWhiteSpace(document))
                    continue;

                chunks.Add(new RagChunk(
                    ValueAt(result.Ids, batch, i) ?? $"chunk-{batch}-{i}",
                    ReadTitle(MetadataAt(result.Metadatas, batch, i)),
                    document));
            }
        }

        return chunks;
    }

    private static string ReadTitle(Dictionary<string, JsonElement>? metadata)
    {
        if (metadata is null)
            return RagChunk.UnknownTitle;

        foreach (var key in new[] { "title", "source", "name", "document_title", "file" })
        {
            if (metadata.TryGetValue(key, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }

        return RagChunk.UnknownTitle;
    }

    private static string? ValueAt(List<List<string>?>? batches, int batch, int index) =>
        batches is not null
        && batch < batches.Count
        && batches[batch] is { } values
        && index < values.Count
            ? values[index]
            : null;

    private static Dictionary<string, JsonElement>? MetadataAt(
        List<List<Dictionary<string, JsonElement>?>?>? batches,
        int batch,
        int index) =>
        batches is not null
        && batch < batches.Count
        && batches[batch] is { } values
        && index < values.Count
            ? values[index]
            : null;

    /// <summary>
    /// Development helper: upsert documents into the knowledge collection.
    /// Tries /add then /upsert against the local Chroma proxy. Returns 0 on failure.
    /// </summary>
    public async Task<int> UpsertDocumentsAsync(
        string collectionName,
        IReadOnlyList<ChromaSeedDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return 0;

        var payload = new ChromaUpsertRequest
        {
            CollectionName = collectionName,
            Ids = documents.Select(d => d.Id).ToArray(),
            Documents = documents.Select(d => d.Document).ToArray(),
            Metadatas = documents.Select(d => d.Metadata).ToArray()
        };

        foreach (var path in new[] { "/add", "/upsert" })
        {
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    path,
                    payload,
                    JsonOptions,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return documents.Count;
            }
            catch (HttpRequestException)
            {
                // try next path / soft-fail
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // timeout — soft-fail
            }
        }

        return 0;
    }
}

public sealed class ChromaLookupResult
{
    public bool IsAvailable { get; private init; }
    public string Content { get; private init; } = string.Empty;

    /// <summary>The retrieved passages, kept so callers can cite them by source.</summary>
    public IReadOnlyList<RagChunk> Chunks { get; private init; } = [];

    public static ChromaLookupResult Ok(IReadOnlyList<RagChunk> chunks) =>
        new()
        {
            IsAvailable = true,
            Chunks = chunks,
            Content = string.Join(
                Environment.NewLine + Environment.NewLine,
                chunks.Select(c => c.Document.Trim()))
        };

    public static ChromaLookupResult Empty() =>
        new() { IsAvailable = true, Content = string.Empty };

    public static ChromaLookupResult Unavailable() =>
        new() { IsAvailable = false, Content = ClientErrorSanitizer.ServiceUnavailableMessage };
}

public class ChromaUpsertRequest
{
    [JsonPropertyName("collection_name")]
    public string CollectionName { get; set; } = string.Empty;

    [JsonPropertyName("ids")]
    public string[] Ids { get; set; } = [];

    [JsonPropertyName("documents")]
    public string[] Documents { get; set; } = [];

    [JsonPropertyName("metadatas")]
    public Dictionary<string, object>[] Metadatas { get; set; } = [];
}

public class ChromaQueryRequest
{
    [JsonPropertyName("query_texts")]
    public string[] QueryTexts { get; set; } = [];

    [JsonPropertyName("n_results")]
    public int NResults { get; set; }

    [JsonPropertyName("collection_name")]
    public string CollectionName { get; set; } = string.Empty;
}

public class ChromaHttpQueryResult
{
    [JsonPropertyName("documents")]
    public List<List<string>?> Documents { get; set; } = new();

    [JsonPropertyName("ids")]
    public List<List<string>?>? Ids { get; set; }

    [JsonPropertyName("metadatas")]
    public List<List<Dictionary<string, JsonElement>?>?>? Metadatas { get; set; }
}
