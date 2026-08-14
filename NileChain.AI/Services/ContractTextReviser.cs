using NileChain.AI.Agents;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;

namespace NileChain.AI.Services;

public sealed class ContractTextReviser : IContractTextReviser
{
    private readonly Lazy<ContractAgent> _contractAgent;

    public ContractTextReviser(Lazy<ContractAgent> contractAgent)
    {
        _contractAgent = contractAgent;
    }

    public async Task<Result<string>> ReviseAsync(
        string currentContractText,
        string changeInstructions,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var result = await _contractAgent.Value.ReviseContractAsync(
            currentContractText,
            changeInstructions);

        if (!result.Success || string.IsNullOrWhiteSpace(result.ContractText))
        {
            return Result<string>.Failure(
                new Error(
                    result.ErrorCode ?? FactoryErrors.AiRevisionUnavailable.Code,
                    string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? FactoryErrors.AiRevisionUnavailable.Description
                        : result.ErrorMessage!));
        }

        return Result<string>.Success(result.ContractText.Trim());
    }
}
