using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class ContractIntegrityRepository : IContractIntegrityRepository
{
    private readonly NileChainDbContext _db;

    public ContractIntegrityRepository(NileChainDbContext db) => _db = db;

    public Task<ContractIntegrityAnchor?> GetChainHeadAsync() =>
        _db.ContractIntegrityAnchors
            .OrderByDescending(a => a.ChainIndex)
            .FirstOrDefaultAsync();

    public Task<ContractIntegrityAnchor?> GetByContentHashAsync(string contentHash)
    {
        var hash = (contentHash ?? string.Empty).Trim().ToLowerInvariant();
        return _db.ContractIntegrityAnchors
            .Include(a => a.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.Farm)
            .Include(a => a.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.SupplyRequest)
                        .ThenInclude(r => r.Factory)
            .Include(a => a.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.SupplyRequest)
                        .ThenInclude(r => r.CropType)
            .FirstOrDefaultAsync(a => a.ContentHash == hash);
    }

    public Task<ContractIntegrityAnchor?> GetActiveForContractAsync(Guid contractId) =>
        _db.ContractIntegrityAnchors
            .Where(a => a.ContractId == contractId && a.Status == ContractIntegrityAnchorStatus.Active)
            .OrderByDescending(a => a.ChainIndex)
            .FirstOrDefaultAsync();

    public Task<List<ContractIntegrityAnchor>> GetActiveAnchorsForContractAsync(Guid contractId) =>
        _db.ContractIntegrityAnchors
            .Where(a => a.ContractId == contractId && a.Status == ContractIntegrityAnchorStatus.Active)
            .ToListAsync();

    public async Task AddAsync(ContractIntegrityAnchor anchor) =>
        await _db.ContractIntegrityAnchors.AddAsync(anchor);

    public void Update(ContractIntegrityAnchor anchor) =>
        _db.ContractIntegrityAnchors.Update(anchor);
}
