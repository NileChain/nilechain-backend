namespace NileChain.Domain.Enums;

public enum FulfillmentStatus
{
    Planned,
    Shipped,
    Received,
    QualityChecked,
    Fulfilled,
    /// <summary>Terminal — factory refused the load at the gate before receive.</summary>
    RejectedAtGate,
    /// <summary>Terminal — contract cancelled or reopened after signing (e.g. text regen).</summary>
    Voided
}
