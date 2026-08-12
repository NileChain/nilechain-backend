namespace NileChain.Application.Dtos.Dispute;

public sealed class DisputeEvidenceDto
{
    public Guid DisputeEvidenceId { get; set; }
    public string FileName { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public long FileSize { get; set; }
    public string FileType { get; set; } = default!;
    public DateTime UploadedAt { get; set; }
}

public sealed class DisputeEventDto
{
    public Guid EventId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = default!;
    public Guid ActorUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class DisputeDto
{
    public Guid DisputeId { get; set; }
    public Guid ContractId { get; set; }
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string RaisedByParty { get; set; } = default!;
    public Guid RaisedByUserId { get; set; }
    public string? AdminNote { get; set; }
    public string OutcomeFavor { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UnderReviewAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? FarmName { get; set; }
    public string? FactoryName { get; set; }
    public bool FulfillmentFrozen { get; set; }
    public List<DisputeEvidenceDto> Evidence { get; set; } = [];
    public List<DisputeEventDto> Events { get; set; } = [];
}

public sealed class DisputeListDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<DisputeDto> Items { get; set; } = [];
}

public sealed class AdminDisputeActionRequest
{
    /// <summary>Required for Resolved / Rejected. Optional note when moving to UnderReview.</summary>
    public string? AdminNote { get; set; }

    /// <summary>Required when resolving: Farm, Factory, or Split. Ignored on reject.</summary>
    public string? OutcomeFavor { get; set; }
}
