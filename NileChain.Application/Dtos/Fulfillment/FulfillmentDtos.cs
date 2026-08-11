using NileChain.Domain.Enums;

namespace NileChain.Application.Dtos.Fulfillment;

public sealed class FulfillmentDto
{
    public Guid FulfillmentId { get; init; }
    public Guid ContractId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime? PlannedShipDate { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public DateTime? QualityCheckedAt { get; init; }
    public DateTime? FulfilledAt { get; init; }
    public DateTime? VoidedAt { get; init; }
    public string? QualityNotes { get; init; }
    public IReadOnlyList<FulfillmentEventDto> Events { get; init; } = Array.Empty<FulfillmentEventDto>();
}

public sealed class FulfillmentEventDto
{
    public Guid EventId { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = default!;
    public Guid ActorUserId { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class StuckFulfillmentDto
{
    public Guid FulfillmentId { get; init; }
    public Guid ContractId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime? PlannedShipDate { get; init; }
    public string? FarmName { get; init; }
    public string? FactoryName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class StuckFulfillmentListDto
{
    public IReadOnlyList<StuckFulfillmentDto> Items { get; init; } = Array.Empty<StuckFulfillmentDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class QualityCheckRequest
{
    public string? Notes { get; set; }
}
