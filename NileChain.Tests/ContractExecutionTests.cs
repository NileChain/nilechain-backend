using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class ContractExecutionTests
{
    [Fact]
    public void AcceptMatchIfFullySigned_BothOrders_AcceptProposedMatch()
    {
        var match = new FarmMatch
        {
            MatchId = Guid.NewGuid(),
            Status = FarmMatchStatus.Proposed
        };

        // Factory signed first, farm second
        var contract = new Contract
        {
            MatchId = match.MatchId,
            FarmMatch = match,
            Status = ContractStatus.PendingFarmSignature,
            FactorySignedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        contract.FarmSignedAt = DateTime.UtcNow;
        contract.RefreshSignatureStatus();
        ContractExecution.AcceptMatchIfFullySigned(contract);
        Assert.Equal(FarmMatchStatus.Accepted, match.Status);
        Assert.Equal(ContractStatus.Signed, contract.Status);

        // Farm signed first, factory second
        var match2 = new FarmMatch
        {
            MatchId = Guid.NewGuid(),
            Status = FarmMatchStatus.Proposed
        };
        var contract2 = new Contract
        {
            MatchId = match2.MatchId,
            FarmMatch = match2,
            Status = ContractStatus.PendingFactorySignature,
            FarmSignedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        contract2.FactorySignedAt = DateTime.UtcNow;
        contract2.RefreshSignatureStatus();
        ContractExecution.AcceptMatchIfFullySigned(contract2);
        Assert.Equal(FarmMatchStatus.Accepted, match2.Status);
    }

    [Fact]
    public void ReplaceGeneratedText_ClearsSignatures_AndResetsStatus()
    {
        var match = new FarmMatch { Status = FarmMatchStatus.Proposed };
        var contract = new Contract
        {
            Status = ContractStatus.PendingFarmSignature,
            FactorySignedAt = DateTime.UtcNow,
            GeneratedText = "old",
            FarmMatch = match
        };

        Assert.True(ContractExecution.TryReplaceGeneratedText(contract, match, "new legal text"));

        Assert.Equal("new legal text", contract.GeneratedText);
        Assert.Null(contract.FactorySignedAt);
        Assert.Null(contract.FarmSignedAt);
        Assert.Equal(ContractStatus.PendingSignature, contract.Status);
        Assert.Equal(FarmMatchStatus.Proposed, match.Status);
    }

    [Fact]
    public void RejectMatchIfProposed_SetsRejected()
    {
        var match = new FarmMatch { Status = FarmMatchStatus.Proposed };
        var contract = new Contract { FarmMatch = match, Status = ContractStatus.Cancelled };
        ContractExecution.RejectMatchIfProposed(contract);
        Assert.Equal(FarmMatchStatus.Rejected, match.Status);
    }

    [Fact]
    public void RejectThenReplace_FailsCleanly_NeverRejectedPlusSigned()
    {
        var match = new FarmMatch
        {
            MatchId = Guid.NewGuid(),
            Status = FarmMatchStatus.Rejected
        };
        var contract = new Contract
        {
            MatchId = match.MatchId,
            FarmMatch = match,
            Status = ContractStatus.Cancelled,
            GeneratedText = "cancelled body"
        };

        Assert.False(ContractExecution.TryReplaceGeneratedText(contract, match, "reopen attempt"));
        Assert.Equal(FarmMatchStatus.Rejected, match.Status);
        Assert.Equal(ContractStatus.Cancelled, contract.Status);
        Assert.Equal("cancelled body", contract.GeneratedText);

        Assert.False(ContractExecution.CanSign(match));
        Assert.False(ContractExecution.CanCreateContract(match));
    }

    [Theory]
    [InlineData(FarmMatchStatus.Rejected)]
    [InlineData(FarmMatchStatus.Expired)]
    [InlineData(FarmMatchStatus.Accepted)]
    public void CanCreateContract_OnlyWhenProposed(FarmMatchStatus status)
    {
        var match = new FarmMatch { Status = status };
        Assert.False(ContractExecution.CanCreateContract(match));
        Assert.Equal(status == FarmMatchStatus.Accepted,
            ContractExecution.CanReplaceText(match));
        Assert.False(ContractExecution.CanSign(match));
    }

    [Fact]
    public void RegenAfterFullySigned_ReopensMatchToProposed()
    {
        var match = new FarmMatch
        {
            MatchId = Guid.NewGuid(),
            Status = FarmMatchStatus.Accepted
        };
        var contract = new Contract
        {
            MatchId = match.MatchId,
            FarmMatch = match,
            Status = ContractStatus.Signed,
            FactorySignedAt = DateTime.UtcNow.AddHours(-1),
            FarmSignedAt = DateTime.UtcNow.AddMinutes(-30),
            SignedAt = DateTime.UtcNow.AddMinutes(-30),
            GeneratedText = "fully signed text"
        };

        Assert.True(ContractExecution.TryReplaceGeneratedText(contract, match, "regenerated text"));

        Assert.Equal(FarmMatchStatus.Proposed, match.Status);
        Assert.Equal(ContractStatus.PendingSignature, contract.Status);
        Assert.Null(contract.FactorySignedAt);
        Assert.Null(contract.FarmSignedAt);
        Assert.Null(contract.SignedAt);
        Assert.Equal("regenerated text", contract.GeneratedText);
        Assert.True(ContractExecution.CanSign(match));
    }

    [Fact]
    public void AcceptMatchIfFullySigned_DoesNotAcceptRejectedMatch()
    {
        var match = new FarmMatch { Status = FarmMatchStatus.Rejected };
        var contract = new Contract
        {
            FarmMatch = match,
            FactorySignedAt = DateTime.UtcNow,
            FarmSignedAt = DateTime.UtcNow,
            Status = ContractStatus.Signed
        };

        ContractExecution.AcceptMatchIfFullySigned(contract);
        Assert.Equal(FarmMatchStatus.Rejected, match.Status);
    }
}
