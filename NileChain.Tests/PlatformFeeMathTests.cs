using NileChain.Application.Contracts;

namespace NileChain.Tests;

public class PlatformFeeMathTests
{
    [Fact]
    public void DealHold_IncludesFee_SoTwoMilestonesFit()
    {
        const decimal deal = 10000m;
        const decimal percent = 2.5m;

        var hold = PlatformFeeMath.TotalCharged(deal, percent);
        Assert.Equal(10250m, hold);

        var deposit = PlatformFeeMath.TotalCharged(3000m, percent);
        var onDelivery = PlatformFeeMath.TotalCharged(7000m, percent);
        Assert.Equal(3075m, deposit);
        Assert.Equal(7175m, onDelivery);
        Assert.Equal(hold, deposit + onDelivery);
    }

    [Fact]
    public void FeeOn_RoundsAwayFromZero()
    {
        Assert.Equal(75m, PlatformFeeMath.FeeOn(3000m, 2.5m));
        Assert.Equal(0m, PlatformFeeMath.FeeOn(1000m, 0m));
        Assert.Equal(300m, PlatformFeeMath.FeeOn(1000m, 50m)); // clamped to 30%
    }
}
