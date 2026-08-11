using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NileChain.AI.Sbg;

/// <summary>
/// Student Bedrock Gateway (SBG) client — POST /api/v1/student/chat with Bearer SBG_API_KEY.
/// Not OpenAI-compatible; uses model_id + system_prompt + messages[].content.
/// </summary>
public sealed class SbgStudentChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly SbgOptions _options;
    private readonly ILogger<SbgStudentChatClient> _logger;

    public SbgStudentChatClient(
        HttpClient http,
        IOptions<SbgOptions> options,
        ILogger<SbgStudentChatClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ResolveOrigin())
        && !string.IsNullOrWhiteSpace(ResolveApiKey());

    public string? UnavailableReason
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResolveOrigin()))
                return "SBG BaseUrl is not set (Sbg:BaseUrl / SBG_BASE_URL).";
            if (string.IsNullOrWhiteSpace(ResolveApiKey()))
                return "SBG API key is not set (SBG_API_KEY).";
            return null;
        }
    }

    public string ModelId =>
        string.IsNullOrWhiteSpace(_options.ModelId)
            ? "amazon.nova-lite-v1:0"
            : _options.ModelId;

    public async Task<string> ChatAsync(
        string? systemPrompt,
        IReadOnlyList<SbgChatMessage> messages,
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(UnavailableReason ?? "SBG is not configured.");

        var key = ResolveApiKey()!;
        var path = string.IsNullOrWhiteSpace(_options.ChatPath)
            ? "/api/v1/student/chat"
            : _options.ChatPath;

        var payload = new SbgChatRequest
        {
            ModelId = ModelId,
            Messages = messages.ToList(),
            SystemPrompt = systemPrompt,
            MaxTokens = maxTokens ?? _options.MaxTokens
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        _logger.LogInformation(
            "SBG chat → {Url} model={Model} messages={Count}",
            request.RequestUri,
            ModelId,
            messages.Count);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "SBG chat failed HTTP {Status}: {Body}",
                (int)response.StatusCode,
                Truncate(body, 500));
            throw new HttpRequestException(
                $"SBG chat failed HTTP {(int)response.StatusCode}: {Truncate(body, 300)}");
        }

        var text = ExtractAssistantText(body);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("SBG chat returned empty text. Raw: {Body}", Truncate(body, 500));
            throw new InvalidOperationException(
                "SBG chat returned an empty assistant message. Check response schema.");
        }

        return text.Trim();
    }

    private Uri BuildUri(string path)
    {
        var baseUrl = ResolveOrigin();
        if (!path.StartsWith('/'))
            path = "/" + path;
        return new Uri(baseUrl + path);
    }

    private string ResolveOrigin()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException(
                "Sbg:BaseUrl is required (set SBG_BASE_URL or appsettings Sbg:BaseUrl).");
        return LlmKernelFactory.NormalizeSbgOrigin(_options.BaseUrl);
    }

    private string? ResolveApiKey() =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("SBG_API_KEY"),
            _options.ApiKey);

    internal static string ExtractAssistantText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (TryGetString(root, out var direct, "output_text", "content", "response", "output", "text", "answer", "message"))
        {
            // "message" may be an object
            if (direct is not null)
                return direct;
        }

        if (root.TryGetProperty("message", out var messageEl))
        {
            if (messageEl.ValueKind == JsonValueKind.String)
                return messageEl.GetString() ?? string.Empty;
            if (TryGetString(messageEl, out var nested, "content", "text") && nested is not null)
                return nested;
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (TryGetString(data, out var fromData, "content", "response", "text", "output") && fromData is not null)
                return fromData;
            if (data.TryGetProperty("message", out var dataMsg)
                && TryGetString(dataMsg, out var dataMsgText, "content", "text")
                && dataMsgText is not null)
                return dataMsgText;
        }

        // Anthropic-ish: content: [ { "text": "..." } ]
        if (root.TryGetProperty("content", out var contentArr)
            && contentArr.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in contentArr.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                    sb.Append(part.GetString());
                else if (TryGetString(part, out var partText, "text", "content") && partText is not null)
                    sb.Append(partText);
            }

            if (sb.Length > 0)
                return sb.ToString();
        }

        // OpenAI-ish choices[0].message.content
        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var choiceMsg)
                && TryGetString(choiceMsg, out var choiceText, "content")
                && choiceText is not null)
                return choiceText;
            if (TryGetString(first, out var choiceDirect, "text") && choiceDirect is not null)
                return choiceDirect;
        }

        return string.Empty;
    }

    private static bool TryGetString(
        JsonElement el,
        out string? value,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

public sealed class SbgChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal sealed class SbgChatRequest
{
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<SbgChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
}
