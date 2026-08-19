using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NileChain.AI;
using NileChain.AI.Agents;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Sbg;
using NileChain.AI.Telemetry;
using NileChain.AI.Verification;

// Guardrail unit smoke (no network)
OrchestrationGuardrailSmoke.Run();

var results = new List<(string Check, bool Pass, string Detail)>();
void Record(string check, bool pass, string detail)
{
    results.Add((check, pass, detail));
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")} | {check} | {detail}");
}

LoadDotEnv(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"));
LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"));
LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Sbg:BaseUrl"] = Environment.GetEnvironmentVariable("SBG_BASE_URL")
                          ?? "http://apiaccess.iti.net.eg",
        ["Sbg:ApiKey"] = Environment.GetEnvironmentVariable("SBG_API_KEY") ?? "",
        ["Sbg:ModelId"] = Environment.GetEnvironmentVariable("SBG_MODEL_ID")
                          ?? "amazon.nova-lite-v1:0",
        ["Sbg:ChatPath"] = "/api/v1/student/chat",
        ["Sbg:MaxTokens"] = "512",
        ["Chroma:BaseUrl"] = "http://localhost:8001"
    })
    .AddEnvironmentVariables()
    .Build();

var apiKey = LlmKernelFactory.ResolveApiKey(config);
Record(
    "SBG_API_KEY loaded",
    !string.IsNullOrWhiteSpace(apiKey) && apiKey.StartsWith("sbg_", StringComparison.Ordinal),
    string.IsNullOrWhiteSpace(apiKey) ? "missing" : $"prefix={apiKey[..Math.Min(8, apiKey.Length)]}…");

var origin = LlmKernelFactory.ResolveSbgBaseUrl(config);
Record(
    "SBG origin resolved",
    origin.Contains("apiaccess.iti.net.eg", StringComparison.OrdinalIgnoreCase),
    origin);

// --- Direct gateway chat ---
string? gatewayBody = null;
try
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    using var req = new HttpRequestMessage(
        HttpMethod.Post,
        $"{origin.TrimEnd('/')}/api/v1/student/chat");
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    req.Content = new StringContent(
        """
        {
          "model_id": "amazon.nova-lite-v1:0",
          "messages": [{ "role": "user", "content": "Reply with OK only." }],
          "system_prompt": "Be terse.",
          "max_tokens": 32
        }
        """,
        Encoding.UTF8,
        "application/json");
    using var resp = await http.SendAsync(req);
    gatewayBody = await resp.Content.ReadAsStringAsync();
    var ok = resp.IsSuccessStatusCode
             && gatewayBody.Contains("output_text", StringComparison.OrdinalIgnoreCase);
    Record(
        "POST /api/v1/student/chat",
        ok,
        $"HTTP {(int)resp.StatusCode}; body={Truncate(gatewayBody, 180)}");
}
catch (Exception ex)
{
    Record("POST /api/v1/student/chat", false, ex.Message);
}

// --- LlmKernelFactory + SK chat ---
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.Configure<SbgOptions>(o =>
{
    o.BaseUrl = origin;
    o.ApiKey = apiKey ?? "";
    o.ModelId = LlmKernelFactory.ResolveModel(config);
    o.ChatPath = "/api/v1/student/chat";
    o.MaxTokens = 800;
});
services.AddHttpClient<SbgStudentChatClient>();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton<LlmUsageLedger>();
var sp = services.BuildServiceProvider();
var sbg = sp.GetRequiredService<SbgStudentChatClient>();

var usageLedger = sp.GetRequiredService<LlmUsageLedger>();
var pricing = new LlmPricing(config);

var kernel = LlmKernelFactory.CreateKernel(
    config,
    out var unavailable,
    out var nativeTools,
    out var provider,
    sbg,
    usageLedger);

Record(
    "LlmKernelFactory creates SBG kernel",
    kernel is not null && provider == "Sbg" && !nativeTools,
    kernel is null ? unavailable ?? "null kernel" : $"provider={provider}; nativeTools={nativeTools}");

Record(
    "No Mantle/OpenAI tool path in factory",
    provider == "Sbg" && !nativeTools,
    $"provider={provider}");

if (kernel is not null)
{
    try
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory("Reply with exactly: NILECHAIN_OK");
        history.AddUserMessage("Say the required token.");
        var msg = await chat.GetChatMessageContentAsync(history);
        var text = msg.Content ?? "";
        Record(
            "LlmKernelFactory real completion",
            text.Length > 0,
            Truncate(text, 160));
    }
    catch (Exception ex)
    {
        Record("LlmKernelFactory real completion", false, ex.Message);
    }
}
else
{
    Record("LlmKernelFactory real completion", false, "kernel unavailable");
}

// --- ContractAgent via SBG (RAG may be empty if Chroma down) ---
try
{
    var providerKernel = new OpenAiKernelProvider(
        kernel,
        unavailable,
        supportsNativeToolCalling: false,
        providerName: provider);

    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var chromaClient = httpFactory.CreateClient();
    chromaClient.BaseAddress = new Uri("http://localhost:8001");
    var chroma = new ChromaService(chromaClient);
    var rag = new RagPipeline(chroma);
    var contractPlugin = new ContractPlugin();
    var contractAgent = new ContractAgent(
        providerKernel,
        contractPlugin,
        rag,
        config,
        sbg,
        usageLedger,
        sp.GetRequiredService<ILogger<ContractAgent>>());

    var request = new AgentRequest
    {
        RequestId = Guid.Parse("d0c0000a-0001-4000-8000-000000000001"),
        CropType = "Wheat",
        QuantityTons = 100,
        QualitySpecs = "Grade A",
        PricePerTon = 10000,
        DeliveryDate = DateTime.UtcNow.AddMonths(2),
        FactoryGovernorate = "Aswan"
    };
    var farm = new MatchResult
    {
        FarmId = Guid.NewGuid(),
        FarmName = "Smoke Test Farm",
        Governorate = "Aswan",
        MatchScore = 80,
        RiskScore = 70,
        RiskLevel = "Medium",
        IsVerified = true
    };

    var contract = await contractAgent.GenerateContractAsync(request, farm, "Smoke Factory");
    Record(
        "ContractAgent via Student Gateway",
        contract.Success && !string.IsNullOrWhiteSpace(contract.ContractText),
        contract.Success
            ? Truncate(contract.ContractText!, 160)
            : $"{contract.ErrorCode}: {contract.ErrorMessage}");
}
catch (Exception ex)
{
    Record("ContractAgent via Student Gateway", false, ex.Message);
}

// Telemetry: whatever the calls above did must now be visible in the ledger.
var usage = usageLedger.Summarize(pricing);
Record(
    "LlmUsageLedger recorded the live calls",
    usage.Calls > 0 && usage.LlmLatencyMs > 0,
    $"calls={usage.Calls}; providers={usage.Providers ?? "-"}; models={usage.Models ?? "-"}; "
    + $"prompt={usage.PromptTokens?.ToString() ?? "unknown"}; "
    + $"completion={usage.CompletionTokens?.ToString() ?? "unknown"}; "
    + $"latency={usage.LlmLatencyMs}ms; "
    + $"cost={usage.EstimatedCostUsd?.ToString("0.000000") ?? "unpriced"}");

// Static checks
var aiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NileChain.AI"));
var mantleHits = 0;
if (Directory.Exists(aiRoot))
{
    foreach (var file in Directory.EnumerateFiles(aiRoot, "*.cs", SearchOption.AllDirectories))
    {
        var text = File.ReadAllText(file);
        if (text.Contains("bedrock-mantle", StringComparison.OrdinalIgnoreCase)
            || text.Contains("AddOpenAIChatCompletion", StringComparison.Ordinal))
            mantleHits++;
    }
}
Record(
    "No OpenAI/Mantle code paths in NileChain.AI *.cs",
    mantleHits == 0,
    mantleHits == 0 ? "clean" : $"hits={mantleHits}");

var fail = results.Count(r => !r.Pass);
Console.WriteLine();
Console.WriteLine($"SUMMARY: {results.Count - fail}/{results.Count} PASS, {fail} FAIL");
Environment.Exit(fail == 0 ? 0 : 1);

static string Truncate(string s, int max) =>
    string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            continue;
        var idx = line.IndexOf('=');
        var key = line[..idx].Trim();
        var value = line[(idx + 1)..].Trim().Trim('"');
        if (key.Length == 0)
            continue;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}
