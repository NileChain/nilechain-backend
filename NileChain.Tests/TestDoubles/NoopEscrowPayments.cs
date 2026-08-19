using NileChain.Application.Common;
using NileChain.Application.Dtos.Payment;
using NileChain.Application.Interfaces;

namespace NileChain.Tests.TestDoubles;

internal sealed class NoopEscrowPayments : IMockEscrowPaymentService
{
    public bool IsMockGatewayEnabled => false;
    public bool IsGatewayEnabled => false;
    public decimal PlatformFeePercent => 0m;

    public Task<Result<MockEscrowSessionDto>> CreateSessionAsync(
        Guid factoryUserId, Guid contractId, Guid transactionId, string? idempotencyKey = null) =>
        Task.FromResult(Result<MockEscrowSessionDto>.Failure(
            NileChain.Application.Errors.MockEscrowErrors.GatewayDisabled));

    public Task<Result<PaymentMilestoneScheduleDto>> ConfirmPaidAsync(
        Guid factoryUserId, Guid contractId, Guid escrowTransactionId) =>
        Task.FromResult(Result<PaymentMilestoneScheduleDto>.Failure(
            NileChain.Application.Errors.MockEscrowErrors.GatewayDisabled));

    public Task<Result<PaymentMilestoneScheduleDto>> CompleteSimulatorAsync(
        Guid factoryUserId, Guid contractId, Guid escrowTransactionId) =>
        Task.FromResult(Result<PaymentMilestoneScheduleDto>.Failure(
            NileChain.Application.Errors.MockEscrowErrors.GatewayDisabled));

    public Task<Result<PaymentMilestoneScheduleDto>> ApplyPaymobEscrowWebhookAsync(
        string specialReference, string? paymobTransactionId, string? orderId, bool success) =>
        Task.FromResult(Result<PaymentMilestoneScheduleDto>.Failure(
            NileChain.Application.Errors.MockEscrowErrors.NotFound));

    public Task<Result<PaymentMilestoneScheduleDto>> ConfirmReleaseAsync(
        Guid factoryUserId, Guid contractId, Guid escrowTransactionId) =>
        Task.FromResult(Result<PaymentMilestoneScheduleDto>.Failure(
            NileChain.Application.Errors.MockEscrowErrors.GatewayDisabled));

    public Task<Result<PaymentMilestoneScheduleDto>> AdminRefundAsync(
        Guid adminUserId, Guid escrowTransactionId, string reason) =>
        Task.FromResult(Result<PaymentMilestoneScheduleDto>.Failure(
            NileChain.Application.Errors.MockEscrowErrors.NotFound));

    public Task<Result> AdminRefundHeldForContractAsync(
        Guid adminUserId, Guid contractId, string reason) =>
        Task.FromResult(Result.Success());

    public Task<Result<IReadOnlyList<EscrowTransactionDto>>> ListForContractAsync(
        Guid userId, Guid contractId, bool asFarm) =>
        Task.FromResult(Result<IReadOnlyList<EscrowTransactionDto>>.Success(
            Array.Empty<EscrowTransactionDto>()));

    public Task<Result> UnwindSignedDealAsync(Guid contractId, Guid actorUserId, string reason) =>
        Task.FromResult(Result.Success());

    public Task<Result> RefundLeftoverDealHoldAsync(Guid contractId, Guid actorUserId, string reason) =>
        Task.FromResult(Result.Success());

    public Task<Result> ApplyQcAmountAdjustmentAsync(
        Guid contractId, Guid transactionId, decimal previousAmount, decimal newAmount) =>
        Task.FromResult(Result.Success());

    public Task<Result> SettleDisputeOutcomeAsync(
        Guid contractId, Guid actorUserId, string outcomeFavor, string reason) =>
        Task.FromResult(Result.Success());

    public Task<Result<EscrowReconciliationDto>> ListReconciliationAsync() =>
        Task.FromResult(Result<EscrowReconciliationDto>.Success(new EscrowReconciliationDto()));
}
