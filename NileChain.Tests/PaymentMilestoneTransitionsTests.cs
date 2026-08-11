using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class PaymentMilestoneTransitionsTests
{
    [Theory]
    [InlineData(TransactionStatus.Pending, TransactionStatus.MarkedPaid, true)]
    [InlineData(TransactionStatus.MarkedPaid, TransactionStatus.Completed, true)]
    [InlineData(TransactionStatus.Pending, TransactionStatus.Completed, false)]
    [InlineData(TransactionStatus.Completed, TransactionStatus.MarkedPaid, false)]
    [InlineData(TransactionStatus.Voided, TransactionStatus.MarkedPaid, false)]
    public void CanTransition_Matrix(TransactionStatus from, TransactionStatus to, bool expected)
    {
        Assert.Equal(expected, PaymentMilestoneTransitions.CanTransition(from, to));
    }

    [Fact]
    public void RoleGates_FactoryMarksPaid_FarmConfirmsReceived()
    {
        Assert.True(PaymentMilestoneTransitions.IsFactoryAction(TransactionStatus.MarkedPaid));
        Assert.False(PaymentMilestoneTransitions.IsFactoryAction(TransactionStatus.Completed));
        Assert.True(PaymentMilestoneTransitions.IsFarmAction(TransactionStatus.Completed));
        Assert.False(PaymentMilestoneTransitions.IsFarmAction(TransactionStatus.MarkedPaid));
    }

    [Fact]
    public void CanVoid_IncludesCompleted_UnlikeFulfillmentFulfilled()
    {
        Assert.True(PaymentMilestoneTransitions.CanVoid(TransactionStatus.Pending));
        Assert.True(PaymentMilestoneTransitions.CanVoid(TransactionStatus.MarkedPaid));
        Assert.True(PaymentMilestoneTransitions.CanVoid(TransactionStatus.Completed));
        Assert.False(PaymentMilestoneTransitions.CanVoid(TransactionStatus.Voided));
    }
}

public class ContractCommercialTotalTests
{
    [Fact]
    public void TryCompute_UsesQuantityTimesPrice()
    {
        var ok = ContractCommercialTotal.TryCompute(
            new NileChain.Domain.Entities.SupplyRequest
            {
                QuantityTons = 10,
                PricePerTon = 1000
            },
            out var total,
            out var reason);

        Assert.True(ok);
        Assert.Equal(10000m, total);
        Assert.Null(reason);
    }

    [Fact]
    public void TryCompute_MissingPrice_FailsClosed()
    {
        var ok = ContractCommercialTotal.TryCompute(
            new NileChain.Domain.Entities.SupplyRequest
            {
                QuantityTons = 10,
                PricePerTon = null
            },
            out var total,
            out var reason);

        Assert.False(ok);
        Assert.Equal(0m, total);
        Assert.Contains("PricePerTon", reason, StringComparison.OrdinalIgnoreCase);
    }
}
