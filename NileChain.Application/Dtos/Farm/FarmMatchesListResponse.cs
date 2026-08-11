using NileChain.Application.Dtos.Admin;

namespace NileChain.Application.Dtos.Farm;

public class FarmMatchSummaryDto
{
    public int Total { get; set; }
    public int Proposed { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int NewCount { get; set; }
}

/// <summary>
/// Paginated farm matches list. Sort defaults to CreatedAt DESC (newest first).
/// Summary is global for the farm (not limited to the current page).
/// NewMatches is a compact strip (max 5) of recent Proposed offers.
/// </summary>
public class FarmMatchesListResponse : PagedResult<FarmMatchItemDto>
{
    public FarmMatchSummaryDto Summary { get; set; } = new();
    public List<FarmMatchItemDto> NewMatches { get; set; } = new();
}
