using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Interfaces;

public interface IEscrowTransactionRepository
{
    Task<EscrowTransaction?> GetByIdAsync(Guid escrowTransactionId, bool tracking = false);
    Task<EscrowTransaction?> GetActiveByTransactionIdAsync(Guid transactionId);
    Task<IReadOnlyList<EscrowTransaction>> GetByContractIdAsync(Guid contractId);
    Task AddAsync(EscrowTransaction escrow);
    Task UpdateAsync(EscrowTransaction escrow);

    Task<bool> TryAtomicStatusAsync(
        Guid escrowTransactionId,
        EscrowStatus expectedFrom,
        EscrowStatus to,
        DateTime utcNow,
        string? reason = null);
}
