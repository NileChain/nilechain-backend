using System.Net.Http.Json;

namespace NileChain.AI.RAG;

public class ChromaService
{
    private readonly HttpClient _httpClient;
    private const string CollectionName = "nilechain_knowledge";

    public ChromaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> QueryAsync(string query, int nResults = 3)
    {
        try
        {
            var request = new
            {
                query_texts = new[] { query },
                n_results = nResults,
                collection_name = CollectionName
            };

            var response = await _httpClient.PostAsJsonAsync("/query", request);

            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<ChromaQueryResult>();

            var documents = result?.Documents
                .SelectMany(d => d)
                .ToList() ?? [];

            return string.Join("\n\n", documents);
        }
        catch
        {
            // RAG unavailable — continue without context
            return string.Empty;
        }
    }
}

public class ChromaQueryResult
{
    public List<List<string>> Documents { get; set; } = new();
}
