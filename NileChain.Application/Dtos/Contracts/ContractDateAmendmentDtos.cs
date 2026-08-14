namespace NileChain.Application.Dtos.Contracts;

public class ProposeContractDateAmendmentRequest
{
    /// <summary>Optional new effective start (UTC calendar date).</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>Optional new effective end / delivery deadline (UTC calendar date).</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>Short reason (delay, logistics, force majeure follow-up, …).</summary>
    public string? Reason { get; set; }
}

public class ContractDateAmendmentDto
{
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? PendingStartsAt { get; set; }
    public DateTime? PendingEndsAt { get; set; }
    public Guid? ProposedByUserId { get; set; }
    public DateTime? ProposedAt { get; set; }
    public bool HasPendingAmendment { get; set; }
    /// <summary>True when the current caller proposed the pending amendment.</summary>
    public bool ProposedByMe { get; set; }
    /// <summary>True when the current caller can accept/reject the pending amendment.</summary>
    public bool CanRespond { get; set; }
}
