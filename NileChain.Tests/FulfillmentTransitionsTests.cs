using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class FulfillmentTransitionsTests
{
    [Theory]
    [InlineData(FulfillmentStatus.Planned, FulfillmentStatus.Shipped, true)]
    [InlineData(FulfillmentStatus.Shipped, FulfillmentStatus.Received, true)]
    [InlineData(FulfillmentStatus.Shipped, FulfillmentStatus.RejectedAtGate, true)]
    [InlineData(FulfillmentStatus.Received, FulfillmentStatus.RejectedAtGate, false)]
    [InlineData(FulfillmentStatus.RejectedAtGate, FulfillmentStatus.Received, false)]
    [InlineData(FulfillmentStatus.RejectedAtGate, FulfillmentStatus.Voided, false)]
    [InlineData(FulfillmentStatus.Received, FulfillmentStatus.QualityChecked, true)]
    [InlineData(FulfillmentStatus.Received, FulfillmentStatus.Fulfilled, true)]
    [InlineData(FulfillmentStatus.QualityChecked, FulfillmentStatus.Fulfilled, true)]
    [InlineData(FulfillmentStatus.Planned, FulfillmentStatus.Fulfilled, false)]
    [InlineData(FulfillmentStatus.Planned, FulfillmentStatus.Received, false)]
    [InlineData(FulfillmentStatus.Shipped, FulfillmentStatus.Fulfilled, false)]
    [InlineData(FulfillmentStatus.Fulfilled, FulfillmentStatus.Shipped, false)]
    [InlineData(FulfillmentStatus.Voided, FulfillmentStatus.Shipped, false)]
    [InlineData(FulfillmentStatus.Planned, FulfillmentStatus.Voided, true)]
    [InlineData(FulfillmentStatus.Shipped, FulfillmentStatus.Voided, true)]
    [InlineData(FulfillmentStatus.Fulfilled, FulfillmentStatus.Voided, false)]
    public void CanTransition_Matrix(FulfillmentStatus from, FulfillmentStatus to, bool expected)
    {
        Assert.Equal(expected, FulfillmentTransitions.CanTransition(from, to));
    }

    [Fact]
    public void RoleGates_FarmCanOnlyShip_FactoryCannotShip()
    {
        Assert.True(FulfillmentTransitions.IsFarmAction(FulfillmentStatus.Shipped));
        Assert.False(FulfillmentTransitions.IsFarmAction(FulfillmentStatus.Received));
        Assert.False(FulfillmentTransitions.IsFarmAction(FulfillmentStatus.Fulfilled));

        Assert.False(FulfillmentTransitions.IsFactoryAction(FulfillmentStatus.Shipped));
        Assert.True(FulfillmentTransitions.IsFactoryAction(FulfillmentStatus.Received));
        Assert.True(FulfillmentTransitions.IsFactoryAction(FulfillmentStatus.QualityChecked));
        Assert.True(FulfillmentTransitions.IsFactoryAction(FulfillmentStatus.Fulfilled));
        Assert.True(FulfillmentTransitions.IsFactoryAction(FulfillmentStatus.RejectedAtGate));
        Assert.False(FulfillmentTransitions.IsFarmAction(FulfillmentStatus.RejectedAtGate));
    }

    [Fact]
    public void TerminalStates_FulfilledVoidedAndRejectedAtGate()
    {
        Assert.True(FulfillmentTransitions.IsTerminal(FulfillmentStatus.Fulfilled));
        Assert.True(FulfillmentTransitions.IsTerminal(FulfillmentStatus.Voided));
        Assert.True(FulfillmentTransitions.IsTerminal(FulfillmentStatus.RejectedAtGate));
        Assert.False(FulfillmentTransitions.IsTerminal(FulfillmentStatus.Planned));
        Assert.False(FulfillmentTransitions.IsTerminal(FulfillmentStatus.Shipped));
    }
}
