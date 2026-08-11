using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class DisputeTransitionsTests
{
    [Theory]
    [InlineData(DisputeStatus.Open, DisputeStatus.UnderReview, true)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Resolved, true)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Rejected, true)]
    [InlineData(DisputeStatus.Open, DisputeStatus.Resolved, false)]
    [InlineData(DisputeStatus.Open, DisputeStatus.Rejected, false)]
    [InlineData(DisputeStatus.Resolved, DisputeStatus.UnderReview, false)]
    [InlineData(DisputeStatus.Rejected, DisputeStatus.Open, false)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Open, false)]
    public void CanTransition_MatchesExpectedMatrix(DisputeStatus from, DisputeStatus to, bool expected)
    {
        Assert.Equal(expected, DisputeTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(DisputeStatus.Open, true)]
    [InlineData(DisputeStatus.UnderReview, true)]
    [InlineData(DisputeStatus.Resolved, false)]
    [InlineData(DisputeStatus.Rejected, false)]
    public void IsActive_OnlyOpenAndUnderReview(DisputeStatus status, bool expected)
    {
        Assert.Equal(expected, DisputeTransitions.IsActive(status));
    }

    [Fact]
    public void RequiresOutcomeFavor_OnlyForResolved()
    {
        Assert.True(DisputeTransitions.RequiresOutcomeFavor(DisputeStatus.Resolved));
        Assert.False(DisputeTransitions.RequiresOutcomeFavor(DisputeStatus.Rejected));
        Assert.False(DisputeTransitions.RequiresOutcomeFavor(DisputeStatus.UnderReview));
    }
}
