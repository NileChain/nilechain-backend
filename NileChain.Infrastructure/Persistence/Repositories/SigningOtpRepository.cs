using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class SigningOtpRepository : ISigningOtpRepository
{
    private readonly NileChainDbContext _db;

    public SigningOtpRepository(NileChainDbContext db) => _db = db;

    public Task<SigningOtp?> GetLatestUnusedAsync(Guid userId, Guid contractId) =>
        _db.SigningOtps
            .Where(o => o.UserId == userId && o.ContractId == contractId && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

    public Task<List<SigningOtp>> GetUnusedForUserContractAsync(Guid userId, Guid contractId) =>
        _db.SigningOtps
            .Where(o => o.UserId == userId && o.ContractId == contractId && !o.IsUsed)
            .ToListAsync();

    public async Task AddAsync(SigningOtp otp) =>
        await _db.SigningOtps.AddAsync(otp);

    public Task<Contract?> GetContractForPartyAsync(Guid contractId, Guid userId) =>
        _db.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
                    .ThenInclude(f => f.User)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r.Factory)
                        .ThenInclude(f => f.User)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r.CropType)
            .FirstOrDefaultAsync(c =>
                c.ContractId == contractId
                && (c.FarmMatch.Farm.UserId == userId
                    || c.FarmMatch.SupplyRequest.Factory.UserId == userId));
}
