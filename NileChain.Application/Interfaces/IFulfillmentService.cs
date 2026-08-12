using NileChain.Application.Common;
using NileChain.Application.Dtos.Fulfillment;
using NileChain.Domain.Enums;

namespace NileChain.Application.Interfaces;

public interface IFulfillmentService
{
    Task<Result<FulfillmentDto>> GetByContractAsync(Guid userId, Guid contractId, bool asFarm);
    Task<Result<FulfillmentDto>> MarkShippedAsync(
        Guid farmUserId,
        Guid contractId,
        ShipFulfillmentRequest? request = null);
    Task<Result<FulfillmentDto>> MarkReceivedAsync(
        Guid factoryUserId,
        Guid contractId,
        ReceiveFulfillmentRequest? request = null);
    Task<Result<FulfillmentDto>> MarkRejectedAtGateAsync(
        Guid factoryUserId,
        Guid contractId,
        RejectAtGateRequest request);
    Task<Result<FulfillmentDto>> MarkQualityCheckedAsync(
        Guid factoryUserId,
        Guid contractId,
        QualityCheckRequest? request = null);
    Task<Result<FulfillmentDto>> MarkFulfilledAsync(Guid factoryUserId, Guid contractId);

    /// <summary>
    /// Idempotent create after full signature. Safe under concurrent second-signer races
    /// (unique ContractId + duplicate-key → return existing).
    /// </summary>
    Task EnsureCreatedForSignedContractAsync(
        Guid contractId,
        Guid actorUserId,
        DateTime? plannedShipDate = null,
        DeliveryPoint? deliveryPoint = null,
        DealParty? freightPayer = null,
        DealParty? transitRisk = null);

    /// <summary>Marks active fulfillment Voided when contract is cancelled or reopened.</summary>
    Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason);

    Task<Result<StuckFulfillmentListDto>> GetStuckDeliveriesAsync(int page, int pageSize);
}
