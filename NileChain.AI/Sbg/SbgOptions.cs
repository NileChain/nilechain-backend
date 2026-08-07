namespace NileChain.AI.Sbg;

public sealed class SbgOptions
{
    public const string SectionName = "Sbg";

    /// <summary>
    /// Absolute origin only, e.g. http://apiaccess.iti.net.eg
    /// (also accepts .../api/v1 — stripped by LlmKernelFactory.NormalizeSbgOrigin).
    /// Set via Sbg:BaseUrl / SBG_BASE_URL / appsettings.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;


    /// <summary>Bearer key (also read from SBG_API_KEY / OPENAI_KEY env).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Approved Bedrock-style model id.</summary>
    public string ModelId { get; set; } = "amazon.nova-lite-v1:0";

    public int MaxTokens { get; set; } = 2048;

    public string ChatPath { get; set; } = "/api/v1/student/chat";
}
