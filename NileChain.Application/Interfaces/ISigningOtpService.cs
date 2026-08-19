using NileChain.Application.Common;
using NileChain.Application.Dtos.Signing;
using NileChain.Domain.Entities;

namespace NileChain.Application.Interfaces;

public interface ISigningOtpService
{
    Task<Result<SigningOtpResponse>> SendOtpAsync(
        Guid contractId,
        Guid userId,
        string? ipAddress);

    /// <summary>
    /// Marks the matching unused OTP as used in the change tracker. Caller must SaveChanges.
    /// </summary>
    Task<Result> VerifyAndConsumeAsync(Guid contractId, Guid userId, string? otpCode);
}

public interface IContractHashService
{
    string ComputeHash(Contract contract);
    string ComputeSignatureToken(string hash, Guid userId, DateTime signedAt);
    bool VerifySignatureToken(string hash, Guid userId, DateTime signedAt, string token);
    Task<Result<ContractVerificationDto>> VerifyAsync(Guid contractId, Guid requesterId, bool isAdmin);
}
