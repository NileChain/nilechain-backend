using Microsoft.SemanticKernel;

namespace NileChain.AI;

/// <summary>
/// Holds an optional Semantic Kernel instance. Missing LLM config does not throw at DI time.
/// </summary>
public sealed class OpenAiKernelProvider
{
    public OpenAiKernelProvider(
        Kernel? kernel,
        string? unavailableReason = null,
        bool supportsNativeToolCalling = false,
        string providerName = "None")
    {
        Kernel = kernel;
        UnavailableReason = unavailableReason;
        SupportsNativeToolCalling = supportsNativeToolCalling;
        ProviderName = providerName;
    }

    public Kernel? Kernel { get; }

    public bool IsAvailable => Kernel is not null;

    public string? UnavailableReason { get; }

    /// <summary>True for OpenAI-compatible connectors with FunctionChoiceBehavior; false for SBG.</summary>
    public bool SupportsNativeToolCalling { get; }

    public string ProviderName { get; }
}
