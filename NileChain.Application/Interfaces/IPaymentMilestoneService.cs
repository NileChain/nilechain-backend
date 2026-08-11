using NileChain.Application.Common;
using NileChain.Application.Dtos.Payment;
using NileChain.Domain.Entities;

namespace NileChain.Application.Interfaces;

public interface IPaymentMilestoneService
{
    Task<Result<PaymentMilestoneScheduleDto>> GetByContractAsync(
        Guid userId,
        Guid contractId,
        bool asFarm);

    Task<Result<PaymentMilestoneScheduleDto>> MarkPaidAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid transactionId);

    Task<Result<PaymentMilestoneScheduleDto>> ConfirmReceivedAsync(
        Guid farmUserId,
        Guid contractId,
        Guid transactionId);

    /// <summary>
    /// Idempotent create after full signature. Safe under concurrent second-signer races
    /// (unique ContractId+ScheduleGeneration+Sequence + duplicate-key → treat as success).
    /// Skips creation when QuantityTons × PricePerTon cannot be computed (no silent Amount).
    /// </summary>
    Task EnsureCreatedForSignedContractAsync(
        Guid contractId,
        Guid actorUserId,
        SupplyRequest? supplyRequest);

    /// <summary>
    /// Voids all non-voided milestones when contract text is regenerated or cancelled.
    /// Includes Completed — Amount is qty×price-derived and would go stale after regen.
    /// </summary>
    Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason);
}
