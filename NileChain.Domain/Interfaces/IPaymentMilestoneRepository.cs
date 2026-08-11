using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Interfaces;

public interface IPaymentMilestoneRepository
{
    Task<IReadOnlyList<Transaction>> GetByContractIdAsync(Guid contractId, bool includeEvents = true);
    Task<Transaction?> GetByIdAsync(Guid transactionId, bool includeEvents = true);
    Task<bool> HasActiveScheduleAsync(Guid contractId);
    Task<int> GetMaxScheduleGenerationAsync(Guid contractId);
    Task AddRangeAsync(IEnumerable<Transaction> milestones);
    Task AddEventAsync(TransactionEvent transactionEvent);
    Task AddEventsAsync(IEnumerable<TransactionEvent> events);

    Task<bool> TryAtomicTransitionAsync(
        Guid transactionId,
        TransactionStatus expectedFrom,
        TransactionStatus to,
        DateTime utcNow,
        bool requireNoActiveDispute = false);
}
