using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class ContractSignatureRepository : IContractSignatureRepository
{
    private readonly NileChainDbContext _db;

    public ContractSignatureRepository(NileChainDbContext db) => _db = db;

    public async Task AddSignatureAsync(ContractSignatureRecord record) =>
        await _db.ContractSignatureRecords.AddAsync(record);

    public async Task AddAuditAsync(ContractAuditLog log) =>
        await _db.ContractAuditLogs.AddAsync(log);

    public Task<ContractAuditLog?> GetLatestAuditAsync(Guid contractId) =>
        _db.ContractAuditLogs
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync();

    public Task<List<ContractSignatureRecord>> GetSignaturesForContractAsync(Guid contractId) =>
        _db.ContractSignatureRecords
            .Where(s => s.ContractId == contractId)
            .OrderBy(s => s.SignedAt)
            .ToListAsync();

    public Task<List<ContractAuditLog>> GetAuditTrailAsync(Guid contractId) =>
        _db.ContractAuditLogs
            .Where(a => a.ContractId == contractId)
            .OrderBy(a => a.Timestamp)
            .ThenBy(a => a.Id)
            .ToListAsync();

    public Task<Contract?> GetContractWithPartiesAsync(Guid contractId) =>
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
            .FirstOrDefaultAsync(c => c.ContractId == contractId);
}
