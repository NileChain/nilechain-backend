using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public async Task<string> QueryAsync(string query, int nResults = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        try
        {
            var request = new ChromaQueryRequest
            {
                QueryTexts = [query],
                NResults = nResults,
                CollectionName = CollectionName
            };

            // BaseAddress is configured via DI (default http://localhost:8001)
            var response = await _httpClient.PostAsJsonAsync("/query", request, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var result = await response.Content
                .ReadFromJsonAsync<ChromaQueryResult>(JsonOptions);

            if (result?.Documents is null || result.Documents.Count == 0)
                return string.Empty;

            var documents = result.Documents
                .SelectMany(batch => batch ?? Enumerable.Empty<string>())
                .Where(doc => !string.IsNullOrWhiteSpace(doc))
                .ToList();

            if (documents.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine + Environment.NewLine, documents);
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
        catch (TaskCanceledException)
        {
            // Covers HttpClient timeouts
            return string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
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

public class ChromaQueryResult
{
    [JsonPropertyName("documents")]
    public List<List<string>> Documents { get; set; } = new();
}
