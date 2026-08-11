using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class MatchListOrderingTests
{
    private static IQueryable<FarmMatch> Sample()
    {
        var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var c = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        return new List<FarmMatch>
        {
            new()
            {
                MatchId = a,
                CreatedAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
                MatchScore = 90,
                Status = FarmMatchStatus.Proposed
            },
            new()
            {
                MatchId = b,
                CreatedAt = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
                MatchScore = 70,
                Status = FarmMatchStatus.Accepted
            },
            new()
            {
                MatchId = c,
                CreatedAt = new DateTime(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc),
                MatchScore = 95,
                Status = FarmMatchStatus.Proposed
            }
        }.AsQueryable();
    }

    [Fact]
    public void Default_IsNewestFirst_ByCreatedAt()
    {
        var ordered = MatchListOrdering.Apply(Sample(), null).ToList();
        Assert.Equal(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ordered[0].MatchId);
        Assert.Equal(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            ordered[1].MatchId);
        Assert.Equal(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ordered[2].MatchId);
    }

    [Fact]
    public void Oldest_IsAscendingCreatedAt()
    {
        var ordered = MatchListOrdering.Apply(Sample(), "oldest").ToList();
        Assert.Equal(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ordered[0].MatchId);
    }

    [Fact]
    public void MatchScore_IsHighestFirst()
    {
        var ordered = MatchListOrdering.Apply(Sample(), "matchScore").ToList();
        Assert.Equal(95m, ordered[0].MatchScore);
        Assert.Equal(90m, ordered[1].MatchScore);
        Assert.Equal(70m, ordered[2].MatchScore);
    }
}
