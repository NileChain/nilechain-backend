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

            var documents = result.Documents
                .SelectMany(batch => batch ?? Enumerable.Empty<string>())
                .Where(doc => !string.IsNullOrWhiteSpace(doc))
                .ToList();

            if (documents.Count == 0)
                return ChromaLookupResult.Empty();

            return ChromaLookupResult.Ok(
                string.Join(Environment.NewLine + Environment.NewLine, documents));
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

    public static ChromaLookupResult Ok(string content) =>
        new() { IsAvailable = true, Content = content };

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
    public List<List<string>> Documents { get; set; } = new();
}
