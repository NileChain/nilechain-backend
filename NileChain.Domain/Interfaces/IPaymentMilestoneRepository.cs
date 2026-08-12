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
        bool requireNoActiveDispute = false,
        string? receiptUrl = null,
        string? receiptPublicId = null,
        string? receiptFileName = null);

    /// <summary>
    /// Reduces amount on the first open milestone (Pending, MarkedPaid, or EscrowHeld).
    /// Returns the adjusted transaction id when a row was updated.
    /// </summary>
    Task<(Guid? TransactionId, decimal? PreviousAmount, decimal? NewAmount)> TryApplyDiscountToFirstOpenMilestoneAsync(
        Guid contractId,
        decimal discountPercent,
        Guid actorUserId,
        DateTime utcNow);

    /// <summary>
    /// Scales every still-open milestone amount by <paramref name="factor"/> (payable/contracted).
    /// Open = Pending, MarkedPaid, or EscrowHeld.
    /// </summary>
    Task<IReadOnlyList<(Guid TransactionId, decimal PreviousAmount, decimal NewAmount)>>
        TryScaleOpenMilestonesByFactorAsync(
            Guid contractId,
            decimal factor,
            Guid actorUserId,
            DateTime utcNow);
}
