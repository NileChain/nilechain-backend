using NileChain.Domain.Matching;

namespace NileChain.Application.Dtos.Factory;

/// <summary>
/// One reason a farm reached the shortlist. <see cref="Code"/> is a stable i18n key,
/// so no user-facing sentence is generated on the server.
/// </summary>
public class MatchFactorDto
{
    public string Code { get; set; } = default!;
    public string State { get; set; } = default!;
    public decimal? Points { get; set; }
    public decimal? MaxPoints { get; set; }
    public string? Detail { get; set; }

    public static MatchFactorDto From(MatchFactor factor) => new()
    {
        Code = factor.Code,
        State = factor.State.ToString(),
        Points = factor.Points,
        MaxPoints = factor.MaxPoints,
        Detail = factor.Detail
    };
}
