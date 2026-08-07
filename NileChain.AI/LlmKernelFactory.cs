using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NileChain.AI.Sbg;

namespace NileChain.AI;

/// <summary>
/// Builds a Semantic Kernel for the Student Bedrock Gateway (SBG) student chat API.
/// Does not use Bedrock Mantle / OpenAI Chat Completions.
/// </summary>
public static class LlmKernelFactory
{
    public const string DefaultModel = "amazon.nova-lite-v1:0";

    public static string? ResolveApiKey(IConfiguration configuration) =>
        Environment.GetEnvironmentVariable("SBG_API_KEY")
        ?? Environment.GetEnvironmentVariable("OPENAI_KEY")
        ?? configuration["Sbg:ApiKey"]
        ?? configuration["OpenAI:ApiKey"];

    public static string ResolveModel(IConfiguration configuration) =>
        configuration["Sbg:ModelId"]
        ?? configuration["OpenAI:Model"]
        ?? Environment.GetEnvironmentVariable("SBG_MODEL_ID")
        ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
        ?? DefaultModel;

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
        !string.IsNullOrWhiteSpace(ResolveApiKey(configuration))
        && !string.IsNullOrWhiteSpace(
            FirstNonEmpty(
                configuration["Sbg:BaseUrl"],
                Environment.GetEnvironmentVariable("SBG_BASE_URL")));

    public static Kernel? CreateKernel(
        IConfiguration configuration,
        out string? unavailableReason,
        out bool supportsNativeToolCalling,
        out string providerName,
        SbgStudentChatClient? sbgClient = null)
    {
        unavailableReason = null;
        supportsNativeToolCalling = false;
        providerName = "None";

        if (!IsSbgConfigured(configuration))
        {
            unavailableReason =
                "AI service is unavailable. Set SBG_API_KEY and Sbg:BaseUrl / SBG_BASE_URL.";
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

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
