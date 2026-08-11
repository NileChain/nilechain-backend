using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NileChain.AI.Sbg;

namespace NileChain.AI;

/// <summary>
/// Builds a Semantic Kernel for SBG and OpenAI-compatible providers (OpenAI/Groq/OpenRouter).
/// <c>Llm:Provider=failover</c> tries SBG → OpenAI/Groq → OpenRouter in order.
/// </summary>
public static class LlmKernelFactory
{
    public const string DefaultSbgModel = "amazon.nova-lite-v1:0";
    public const string DefaultOpenAiModel = "gpt-4o-mini";
    public const string DefaultOpenRouterModel = "openrouter/free";
    public const string DefaultOpenRouterEndpoint = "https://openrouter.ai/api/v1";

    public const string ProviderSbg = "sbg";
    public const string ProviderOpenAi = "openai";
    public const string ProviderOpenRouter = "openrouter";

    /// <summary>SBG Bearer key only (never an OpenAI sk- key).</summary>
    public static string? ResolveSbgApiKey(IConfiguration configuration) =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("SBG_API_KEY"),
            configuration["Sbg:ApiKey"]);

    /// <summary>OpenAI / Groq API key.</summary>
    public static string? ResolveOpenAiApiKey(IConfiguration configuration) =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Environment.GetEnvironmentVariable("OPENAI_KEY"),
            configuration["OpenAI:ApiKey"]);

    /// <summary>OpenRouter API key.</summary>
    public static string? ResolveOpenRouterApiKey(IConfiguration configuration) =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPEN_ROUTER_API_KEY"),
            Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            configuration["OpenRouter:ApiKey"]);

    /// <summary>Backward-compatible alias for SBG key resolution.</summary>
    public static string? ResolveApiKey(IConfiguration configuration) =>
        ResolveSbgApiKey(configuration);

    public static string ResolveSbgModel(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["Sbg:ModelId"],
            Environment.GetEnvironmentVariable("SBG_MODEL_ID"))
        ?? DefaultSbgModel;

    public static string ResolveOpenAiModel(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["OpenAI:Model"],
            Environment.GetEnvironmentVariable("OPENAI_MODEL"))
        ?? DefaultOpenAiModel;

    public static string ResolveOpenRouterModel(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["OpenRouter:Model"],
            Environment.GetEnvironmentVariable("OPEN_ROUTER_MODEL"),
            Environment.GetEnvironmentVariable("OPENROUTER_MODEL"))
        ?? DefaultOpenRouterModel;

    public static string ResolveOpenRouterEndpoint(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["OpenRouter:Endpoint"],
            Environment.GetEnvironmentVariable("OPEN_ROUTER_ENDPOINT"),
            Environment.GetEnvironmentVariable("OPENROUTER_ENDPOINT"))
        ?? DefaultOpenRouterEndpoint;

    /// <summary>Legacy helper — prefers SBG model id, then OpenAI model.</summary>
    public static string ResolveModel(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["Sbg:ModelId"],
            configuration["OpenAI:Model"],
            Environment.GetEnvironmentVariable("SBG_MODEL_ID"),
            Environment.GetEnvironmentVariable("OPENAI_MODEL"))
        ?? DefaultSbgModel;

    /// <summary>
    /// Gateway origin host. Accepts either
    /// <c>http://host</c> or <c>http://host/api/v1</c>.
    /// </summary>
    public static string ResolveSbgBaseUrl(IConfiguration configuration)
    {
        var raw = FirstNonEmpty(
            configuration["Sbg:BaseUrl"],
            Environment.GetEnvironmentVariable("SBG_BASE_URL"));

        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "Sbg:BaseUrl / SBG_BASE_URL is required (e.g. http://apiaccess.iti.net.eg).");

        return NormalizeSbgOrigin(raw);
    }

    public static bool IsSbgConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(ResolveSbgApiKey(configuration))
        && !string.IsNullOrWhiteSpace(
            FirstNonEmpty(
                configuration["Sbg:BaseUrl"],
                Environment.GetEnvironmentVariable("SBG_BASE_URL")));

    public static bool IsOpenAiConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(ResolveOpenAiApiKey(configuration));

    public static bool IsOpenRouterConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(ResolveOpenRouterApiKey(configuration));

    /// <summary>
    /// <c>openai</c> | <c>sbg</c> | <c>openrouter</c> | <c>failover</c> / <c>auto</c> (default).
    /// </summary>
    public static string ResolveProviderPreference(IConfiguration configuration)
    {
        var raw = FirstNonEmpty(
            configuration["Llm:Provider"],
            Environment.GetEnvironmentVariable("LLM_PROVIDER"))
            ?? "failover";

        return raw.Trim().ToLowerInvariant() switch
        {
            "openai" or "open_ai" or "oai" or "groq" => ProviderOpenAi,
            "openrouter" or "open_router" or "or" => ProviderOpenRouter,
            "sbg" or "bedrock" or "iti" => ProviderSbg,
            "failover" or "auto" or "fallback" => "failover",
            _ => "failover"
        };
    }

    /// <summary>
    /// Ordered providers to try. Failover: SBG → OpenAI/Groq → OpenRouter.
    /// </summary>
    public static IReadOnlyList<string> ResolveProviderChain(IConfiguration configuration)
    {
        var preference = ResolveProviderPreference(configuration);
        var chain = new List<string>();

        void AddIf(string key, bool configured)
        {
            if (configured)
                chain.Add(key);
        }

        switch (preference)
        {
            case ProviderOpenAi:
                AddIf(ProviderOpenAi, IsOpenAiConfigured(configuration));
                return chain;
            case ProviderOpenRouter:
                AddIf(ProviderOpenRouter, IsOpenRouterConfigured(configuration));
                return chain;
            case ProviderSbg:
                AddIf(ProviderSbg, IsSbgConfigured(configuration));
                return chain;
            default:
                // failover / auto — ITI first, then free OpenRouter, then OpenAI/Groq.
                AddIf(ProviderSbg, IsSbgConfigured(configuration));
                AddIf(ProviderOpenRouter, IsOpenRouterConfigured(configuration));
                AddIf(ProviderOpenAi, IsOpenAiConfigured(configuration));
                return chain;
        }
    }

    public static bool SupportsNativeToolCalling(string providerKey) =>
        string.Equals(providerKey, ProviderOpenAi, StringComparison.OrdinalIgnoreCase)
        || string.Equals(providerKey, ProviderOpenRouter, StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string providerKey) =>
        providerKey.Trim().ToLowerInvariant() switch
        {
            ProviderOpenAi => "OpenAI",
            ProviderOpenRouter => "OpenRouter",
            _ => "Sbg"
        };

    public static Kernel? CreateKernel(
        IConfiguration configuration,
        out string? unavailableReason,
        out bool supportsNativeToolCalling,
        out string providerName,
        SbgStudentChatClient? sbgClient = null)
    {
        var chain = ResolveProviderChain(configuration);
        if (chain.Count == 0)
        {
            unavailableReason =
                "AI service is unavailable. Configure SBG and/or OpenAI/Groq and/or OpenRouter "
                + "(OPEN_ROUTER_API_KEY), with Llm:Provider=failover.";
            supportsNativeToolCalling = false;
            providerName = "None";
            return null;
        }

        return CreateKernelForProvider(
            chain[0],
            configuration,
            out unavailableReason,
            out supportsNativeToolCalling,
            out providerName,
            sbgClient);
    }

    public static Kernel? CreateKernelForProvider(
        string providerKey,
        IConfiguration configuration,
        out string? unavailableReason,
        out bool supportsNativeToolCalling,
        out string providerName,
        SbgStudentChatClient? sbgClient = null)
    {
        if (string.Equals(providerKey, ProviderOpenAi, StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateOpenAiCompatibleKernel(
                displayName: "OpenAI",
                apiKey: ResolveOpenAiApiKey(configuration),
                model: ResolveOpenAiModel(configuration),
                endpoint: FirstNonEmpty(
                    configuration["OpenAI:Endpoint"],
                    Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")),
                missingKeyMessage: "OpenAI/Groq is selected but no key is set (OpenAI:ApiKey / OPENAI_API_KEY).",
                out unavailableReason,
                out supportsNativeToolCalling,
                out providerName);
        }

        if (string.Equals(providerKey, ProviderOpenRouter, StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateOpenAiCompatibleKernel(
                displayName: "OpenRouter",
                apiKey: ResolveOpenRouterApiKey(configuration),
                model: ResolveOpenRouterModel(configuration),
                endpoint: ResolveOpenRouterEndpoint(configuration),
                missingKeyMessage:
                    "OpenRouter is selected but no key is set (OPEN_ROUTER_API_KEY / OpenRouter:ApiKey).",
                out unavailableReason,
                out supportsNativeToolCalling,
                out providerName);
        }

        return TryCreateSbgKernel(
            configuration,
            sbgClient,
            out unavailableReason,
            out supportsNativeToolCalling,
            out providerName);
    }

    /// <summary>
    /// True when the failure looks like a provider outage / quota / network issue
    /// (safe to try the next LLM). False for ordinary app/logic errors.
    /// </summary>
    public static bool IsProviderFailure(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is HttpRequestException or TaskCanceledException or TimeoutException or SocketException)
                return true;

            // OpenAI .NET SDK bug: OpenRouter / OpenAI-compatible payloads can crash while
            // reading ChatCompletion.Refusal (empty ChangeTrackingList → index OOR).
            if (cur is ArgumentOutOfRangeException oor
                && string.Equals(oor.ParamName, "index", StringComparison.OrdinalIgnoreCase))
                return true;

            var text = cur.Message ?? string.Empty;
            if (ContainsAny(
                    text,
                    "429",
                    "502",
                    "503",
                    "504",
                    "rate_limit",
                    "Rate limit",
                    "RATE_LIMITED",
                    "Daily request limit",
                    "timeout",
                    "timed out",
                    "connection",
                    "actively refused",
                    "No connection",
                    "unreachable",
                    "no credits",
                    "insufficient_quota",
                    "SBG chat failed",
                    "Provider returned error",
                    "model_not_found",
                    "not found",
                    "out of the range of valid values"))
                return true;
        }

        return false;
    }

    private static Kernel? TryCreateOpenAiCompatibleKernel(
        string displayName,
        string? apiKey,
        string model,
        string? endpoint,
        string missingKeyMessage,
        out string? unavailableReason,
        out bool supportsNativeToolCalling,
        out string providerName)
    {
        unavailableReason = null;
        supportsNativeToolCalling = false;
        providerName = "None";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            unavailableReason = missingKeyMessage;
            return null;
        }

        var builder = Kernel.CreateBuilder();
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.AddOpenAIChatCompletion(
                modelId: model,
                endpoint: new Uri(endpoint.TrimEnd('/')),
                apiKey: apiKey);
        }
        else
        {
            builder.AddOpenAIChatCompletion(
                modelId: model,
                apiKey: apiKey);
        }

        supportsNativeToolCalling = true;
        providerName = displayName;
        return builder.Build();
    }

    private static Kernel? TryCreateSbgKernel(
        IConfiguration configuration,
        SbgStudentChatClient? sbgClient,
        out string? unavailableReason,
        out bool supportsNativeToolCalling,
        out string providerName)
    {
        unavailableReason = null;
        supportsNativeToolCalling = false;
        providerName = "None";

        if (!IsSbgConfigured(configuration))
        {
            unavailableReason =
                "SBG is selected but not configured (SBG_API_KEY + Sbg:BaseUrl).";
            return null;
        }

        if (sbgClient is null || !sbgClient.IsConfigured)
        {
            unavailableReason =
                sbgClient?.UnavailableReason
                ?? "SBG chat client is unavailable.";
            return null;
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(
            new SbgChatCompletionService(sbgClient));
        supportsNativeToolCalling = false;
        providerName = "Sbg";
        return builder.Build();
    }

    /// <summary>
    /// Strip trailing /api/v1 so callers can pass either origin or gateway base.
    /// </summary>
    public static string NormalizeSbgOrigin(string raw)
    {
        var url = raw.Trim().TrimEnd('/');
        if (url.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
            url = url[..^"/api/v1".Length].TrimEnd('/');
        return url;
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
