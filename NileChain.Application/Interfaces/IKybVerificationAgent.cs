using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;
using NileChain.Domain.Entities;

namespace NileChain.Application.Interfaces;

public interface IKybVerificationAgent
{
    /// <summary>
    /// Compares a farm's uploaded KYB documents (by <see cref="FarmDocument.KybKind"/>)
    /// against KYB-related RAG guidance, returning a trust score and per-kind breakdown.
    /// </summary>
    Task<Result<VerifyUserResult>> VerifyFarmAsync(
        Guid userId,
        Farm farm,
        CancellationToken cancellationToken = default);

    Task<Result<VerifyUserResult>> VerifyFactoryAsync(
        Guid userId,
        Factory factory,
        CancellationToken cancellationToken = default);
}

