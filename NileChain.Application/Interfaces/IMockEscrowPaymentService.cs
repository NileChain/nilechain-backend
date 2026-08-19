using NileChain.Application.Common;
using NileChain.Application.Dtos.Payment;

namespace NileChain.Application.Interfaces;

public interface IMockEscrowPaymentService
{
    bool IsMockGatewayEnabled { get; }
    bool IsGatewayEnabled { get; }
    decimal PlatformFeePercent { get; }

    Task<Result<MockEscrowSessionDto>> CreateSessionAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid transactionId,
        string? idempotencyKey = null);

    Task<Result<PaymentMilestoneScheduleDto>> ConfirmPaidAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid escrowTransactionId);

    Task<Result<PaymentMilestoneScheduleDto>> CompleteSimulatorAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid escrowTransactionId);

    Task<Result<PaymentMilestoneScheduleDto>> ApplyPaymobEscrowWebhookAsync(
        string specialReference,
        string? paymobTransactionId,
        string? orderId,
        bool success);

    Task<Result<PaymentMilestoneScheduleDto>> ConfirmReleaseAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid escrowTransactionId);

    Task<Result<PaymentMilestoneScheduleDto>> AdminRefundAsync(
        Guid adminUserId,
        Guid escrowTransactionId,
        string reason);

    Task<Result> AdminRefundHeldForContractAsync(
        Guid adminUserId,
        Guid contractId,
        string reason);

    Task<Result<IReadOnlyList<EscrowTransactionDto>>> ListForContractAsync(
        Guid userId,
        Guid contractId,
        bool asFarm);

    /// <summary>
    /// Refund remaining deal hold to the factory, mark Held escrows Refunded (no second debit),
    /// and void unpaid milestones. Caller cancels the contract and voids fulfillment.
    /// </summary>
    Task<Result> UnwindSignedDealAsync(Guid contractId, Guid actorUserId, string reason);

    /// <summary>
    /// Refund leftover deal hold and void unpaid milestones without cancelling the contract.
    /// Used for gate rejection. Allowed even with an open dispute.
    /// </summary>
    Task<Result> RefundLeftoverDealHoldAsync(Guid contractId, Guid actorUserId, string reason);

    /// <summary>
    /// After a QC discount (or short-qty implied discount) on a milestone, refund the
    /// charged delta from factory Held and snap escrow amounts if the row is still open.
    /// </summary>
    Task<Result> ApplyQcAmountAdjustmentAsync(
        Guid contractId,
        Guid transactionId,
        decimal previousAmount,
        decimal newAmount);

    /// <summary>
    /// Move money for a resolved dispute: Factory = refund held; Farm = release held to farm;
    /// Split = 50% of farm net to farm, remainder (including fee) back to factory.
    /// Leftover unpaid deal hold is always returned to the factory.
    /// </summary>
    Task<Result> SettleDisputeOutcomeAsync(
        Guid contractId,
        Guid actorUserId,
        string outcomeFavor,
        string reason);

    Task<Result<EscrowReconciliationDto>> ListReconciliationAsync();
}
