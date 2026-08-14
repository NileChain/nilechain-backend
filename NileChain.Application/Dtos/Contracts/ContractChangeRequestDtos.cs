using NileChain.Domain.Entities;

namespace NileChain.Application.Dtos.Contracts;

public class RequestContractChangesRequest
{
    /// <summary>Free-text amendments the party wants applied to the draft.</summary>
    public string Instructions { get; set; } = string.Empty;
}

public class RequestContractChangesResponse
{
    public Guid ContractId { get; set; }
    public string GeneratedText { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ContractRevisionDto? LastRevision { get; set; }
}

public class ContractRevisionDto
{
    public Guid ContractRevisionId { get; set; }
    public string PreviousText { get; set; } = string.Empty;
    public string NewText { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string RevisedByParty { get; set; } = string.Empty;
    public Guid RevisedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public static ContractRevisionDto? Last(IEnumerable<ContractRevision>? revisions)
    {
        var last = revisions?
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();
        return last is null ? null : From(last);
    }

    public static ContractRevisionDto From(ContractRevision revision) => new()
    {
        ContractRevisionId = revision.ContractRevisionId,
        PreviousText = revision.PreviousText,
        NewText = revision.NewText,
        Instructions = revision.Instructions,
        RevisedByParty = revision.RevisedByParty.ToString(),
        RevisedByUserId = revision.RevisedByUserId,
        CreatedAt = revision.CreatedAt
    };
}
