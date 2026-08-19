using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface ISigningOtpRepository
{
    Task<SigningOtp?> GetLatestUnusedAsync(Guid userId, Guid contractId);
    Task<List<SigningOtp>> GetUnusedForUserContractAsync(Guid userId, Guid contractId);
    Task AddAsync(SigningOtp otp);
    Task<Contract?> GetContractForPartyAsync(Guid contractId, Guid userId);
}
