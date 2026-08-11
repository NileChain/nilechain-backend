using NileChain.Application.Common;
using NileChain.Application.Dtos.Fulfillment;
using NileChain.Domain.Enums;

namespace NileChain.Application.Interfaces;

public interface IFulfillmentService
{
    Task<Result<FulfillmentDto>> GetByContractAsync(Guid userId, Guid contractId, bool asFarm);
    Task<Result<FulfillmentDto>> MarkShippedAsync(Guid farmUserId, Guid contractId);
    Task<Result<FulfillmentDto>> MarkReceivedAsync(Guid factoryUserId, Guid contractId);
    Task<Result<FulfillmentDto>> MarkQualityCheckedAsync(Guid factoryUserId, Guid contractId, string? notes);
    Task<Result<FulfillmentDto>> MarkFulfilledAsync(Guid factoryUserId, Guid contractId);

    /// <summary>
    /// Idempotent create after full signature. Safe under concurrent second-signer races
    /// (unique ContractId + duplicate-key → return existing).
    /// </summary>
    Task EnsureCreatedForSignedContractAsync(
        Guid contractId,
        Guid actorUserId,
        DateTime? plannedShipDate = null);

    /// <summary>Marks active fulfillment Voided when contract is cancelled or reopened.</summary>
    Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason);

    Task<Result<StuckFulfillmentListDto>> GetStuckDeliveriesAsync(int page, int pageSize);
}
