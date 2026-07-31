using Microsoft.SemanticKernel;

namespace NileChain.AI;

/// <summary>
/// Holds an optional Semantic Kernel instance. Missing OpenAI config does not throw at DI time.
/// </summary>
public sealed class OpenAiKernelProvider
{
    public OpenAiKernelProvider(Kernel? kernel, string? unavailableReason = null)
    {
        Kernel = kernel;
        UnavailableReason = unavailableReason;
    }

    public Kernel? Kernel { get; }

    public bool IsAvailable => Kernel is not null;

    public string? UnavailableReason { get; }
}
