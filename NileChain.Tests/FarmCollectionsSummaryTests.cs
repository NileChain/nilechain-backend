using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class FarmCollectionsSummaryTests
{
    [Fact]
    public void Aggregate_SumsPendingMarkedPaidAndCompleted_IgnoresVoided()
    {
        var matches = new List<FarmMatch>
        {
            new()
            {
                MatchId = Guid.NewGuid(),
                Contract = new Contract
                {
                    ContractId = Guid.NewGuid(),
                    Status = ContractStatus.Signed,
                    Transactions =
                    [
                        new Transaction { Amount = 1000m, Status = TransactionStatus.Pending },
                        new Transaction { Amount = 500m, Status = TransactionStatus.MarkedPaid },
                        new Transaction { Amount = 2000m, Status = TransactionStatus.Completed },
                        new Transaction { Amount = 999m, Status = TransactionStatus.Voided },
                    ]
                }
            },
            new()
            {
                MatchId = Guid.NewGuid(),
                Contract = new Contract
                {
                    ContractId = Guid.NewGuid(),
                    Status = ContractStatus.Cancelled,
                    Transactions =
                    [
                        new Transaction { Amount = 7000m, Status = TransactionStatus.Pending }
                    ]
                }
            }
        };

        var summary = Aggregate(matches);

        Assert.Equal(1000m, summary.Pending);
        Assert.Equal(500m, summary.Awaiting);
        Assert.Equal(2000m, summary.Received);
    }

    private static (decimal Pending, decimal Awaiting, decimal Received) Aggregate(
        IEnumerable<FarmMatch> matches)
    {
        var transactions = matches
            .Select(m => m.Contract)
            .Where(c => c is not null && c.Status == ContractStatus.Signed)
            .SelectMany(c => c!.Transactions ?? Enumerable.Empty<Transaction>())
            .Where(t => t.Status != TransactionStatus.Voided)
            .ToList();

        return (
            transactions.Where(t => t.Status == TransactionStatus.Pending).Sum(t => t.Amount),
            transactions.Where(t => t.Status == TransactionStatus.MarkedPaid).Sum(t => t.Amount),
            transactions.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.Amount));
    }
}
