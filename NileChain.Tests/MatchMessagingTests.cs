using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class MatchMessagingTests
{
    [Theory]
    [InlineData(FarmMatchStatus.Proposed, true)]
    [InlineData(FarmMatchStatus.Countered, true)]
    [InlineData(FarmMatchStatus.Accepted, true)]
    [InlineData(FarmMatchStatus.Rejected, false)]
    [InlineData(FarmMatchStatus.Expired, false)]
    public void CanMessage_FromProposedUntilAccepted(FarmMatchStatus status, bool expected)
    {
        var match = new FarmMatch { Status = status };
        Assert.Equal(expected, MatchMessaging.CanMessage(match));
    }

    [Fact]
    public void CanMessage_FalseWhenFactoryExcluded()
    {
        var match = new FarmMatch
        {
            Status = FarmMatchStatus.Proposed,
            IsExcludedByFactory = true
        };
        Assert.False(MatchMessaging.CanMessage(match));
        Assert.False(MatchMessaging.CanNegotiate(match));
    }

    [Fact]
    public void CanAddRound_CapsAtEight()
    {
        var match = new FarmMatch();
        Assert.True(MatchMessaging.CanAddRound(match));

        for (var i = 0; i < MatchMessaging.MaxNegotiationRounds; i++)
        {
            match.NegotiationRounds.Add(new MatchNegotiationRound
            {
                RoundId = Guid.NewGuid(),
                OfferedBy = i % 2 == 0 ? DealParty.Farm : DealParty.Factory
            });
        }

        Assert.Equal(8, MatchMessaging.MaxNegotiationRounds);
        Assert.False(MatchMessaging.CanAddRound(match));
    }

    [Fact]
    public void CanAcceptCounter_OnlyOtherPartyLastRound()
    {
        var match = new FarmMatch
        {
            Status = FarmMatchStatus.Countered,
            CounteredAt = DateTime.UtcNow,
            CounterQuantityTons = 30
        };
        match.NegotiationRounds.Add(new MatchNegotiationRound
        {
            RoundId = Guid.NewGuid(),
            OfferedBy = DealParty.Factory,
            QuantityTons = 30,
            CreatedAt = DateTime.UtcNow
        });

        Assert.True(MatchMessaging.CanAcceptCounter(match, DealParty.Farm));
        Assert.False(MatchMessaging.CanAcceptCounter(match, DealParty.Factory));
    }

    [Fact]
    public void CanAcceptCounter_LegacyFarmCounter_FactoryOnly()
    {
        var match = new FarmMatch
        {
            Status = FarmMatchStatus.Countered,
            CounteredAt = DateTime.UtcNow,
            CounterPricePerTon = 12000
        };

        Assert.True(MatchMessaging.CanAcceptCounter(match, DealParty.Factory));
        Assert.False(MatchMessaging.CanAcceptCounter(match, DealParty.Farm));
    }
}
