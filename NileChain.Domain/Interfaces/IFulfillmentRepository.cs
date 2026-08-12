using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Interfaces;

public interface IFulfillmentRepository
{
    Task<Fulfillment?> GetByContractIdAsync(Guid contractId, bool includeEvents = true);
    Task<Fulfillment?> GetByIdAsync(Guid fulfillmentId);
    Task AddAsync(Fulfillment fulfillment);
    Task AddEventAsync(FulfillmentEvent fulfillmentEvent);

    /// <summary>
    /// Single-statement status update: succeeds only when current status equals <paramref name="expectedFrom"/>.
    /// When <paramref name="requireNoActiveDispute"/> is true, also requires no Open/UnderReview dispute
    /// on the fulfillment's contract (same atomic WHERE — not a separate read-then-block).
    /// </summary>
    Task<bool> TryAtomicTransitionAsync(
        Guid fulfillmentId,
        FulfillmentStatus expectedFrom,
        FulfillmentStatus to,
        DateTime utcNow,
        string? qualityNotes = null,
        bool requireNoActiveDispute = false,
        string? carrier = null,
        string? trackingNumber = null,
        string? shippedNotes = null,
        decimal? acceptedQuantityTons = null,
        decimal? discountPercent = null,
        bool? specsMet = null,
        string? specsOutcomeNotes = null,
        decimal? weighedQuantityTons = null,
        string? weighbridgeTicketUrl = null,
        GateRejectReason? gateRejectReason = null,
        string? gateRejectNotes = null,
        DealParty? returnFreightBearer = null);

    Task<IReadOnlyList<Fulfillment>> GetStuckPlannedAsync(DateTime asOfUtcNoon, int skip, int take);
    Task<int> CountStuckPlannedAsync(DateTime asOfUtcNoon);
}
