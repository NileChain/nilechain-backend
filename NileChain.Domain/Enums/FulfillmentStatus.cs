namespace NileChain.Domain.Enums;

public enum FulfillmentStatus
{
    Planned,
    Shipped,
    Received,
    QualityChecked,
    Fulfilled,
    /// <summary>Terminal — contract cancelled or reopened after signing (e.g. text regen).</summary>
    Voided
}
