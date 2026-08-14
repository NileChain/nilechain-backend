using NileChain.Application.Common;

namespace NileChain.Application.Interfaces;

/// <summary>
/// Revises an existing unsigned contract body from free-text party instructions (AI).
/// </summary>
public interface IContractTextReviser
{
    Task<Result<string>> ReviseAsync(
        string currentContractText,
        string changeInstructions,
        CancellationToken cancellationToken = default);
}
