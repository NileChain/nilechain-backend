using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class ContractSigningLifecycleTests
{
    [Fact]
    public void NewContract_HasNoPartySignatures()
    {
        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            MatchId = Guid.NewGuid(),
            Status = ContractStatus.PendingSignature,
            CreatedAt = DateTime.UtcNow
        };

        Assert.False(contract.IsFactorySigned);
        Assert.False(contract.IsFarmSigned);
        Assert.False(contract.IsFullySigned);
        Assert.Null(contract.FactorySignedAt);
        Assert.Null(contract.FarmSignedAt);
        Assert.Equal(ContractStatus.PendingSignature, contract.Status);
    }

    [Fact]
    public void FactorySigns_SetsFactoryOnly_StatusPendingFarmSignature()
    {
        var contract = new Contract
        {
            Status = ContractStatus.PendingSignature,
            CreatedAt = DateTime.UtcNow
        };

        contract.FactorySignedAt = DateTime.UtcNow;
        contract.RefreshSignatureStatus();

        Assert.True(contract.IsFactorySigned);
        Assert.False(contract.IsFarmSigned);
        Assert.Equal(ContractStatus.PendingFarmSignature, contract.Status);
        Assert.Null(contract.SignedAt);
    }

    [Fact]
    public void FarmSignsAlone_SetsFarmOnly_StatusPendingFactorySignature()
    {
        var contract = new Contract
        {
            Status = ContractStatus.PendingSignature,
            CreatedAt = DateTime.UtcNow
        };

        contract.FarmSignedAt = DateTime.UtcNow;
        contract.RefreshSignatureStatus();

        Assert.False(contract.IsFactorySigned);
        Assert.True(contract.IsFarmSigned);
        Assert.Equal(ContractStatus.PendingFactorySignature, contract.Status);
        Assert.Null(contract.SignedAt);
    }

    [Fact]
    public void BothPartiesSign_StatusSigned_AndSignedAtSet()
    {
        var factoryAt = DateTime.UtcNow.AddMinutes(-5);
        var farmAt = DateTime.UtcNow;

        var contract = new Contract
        {
            Status = ContractStatus.PendingSignature,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            FactorySignedAt = factoryAt,
            FarmSignedAt = farmAt
        };

        contract.RefreshSignatureStatus();

        Assert.True(contract.IsFullySigned);
        Assert.Equal(ContractStatus.Signed, contract.Status);
        Assert.Equal(farmAt, contract.SignedAt);
    }

    [Fact]
    public void FactorySign_DoesNotModifyFarmSignedAt()
    {
        var contract = new Contract
        {
            Status = ContractStatus.PendingSignature,
            FarmSignedAt = null
        };

        contract.FactorySignedAt = DateTime.UtcNow;
        contract.RefreshSignatureStatus();

        Assert.Null(contract.FarmSignedAt);
        Assert.True(contract.IsFactorySigned);
    }

    [Fact]
    public void FarmSign_DoesNotModifyFactorySignedAt()
    {
        var factoryAt = DateTime.UtcNow.AddHours(-1);
        var contract = new Contract
        {
            Status = ContractStatus.PendingFarmSignature,
            FactorySignedAt = factoryAt
        };

        contract.FarmSignedAt = DateTime.UtcNow;
        contract.RefreshSignatureStatus();

        Assert.Equal(factoryAt, contract.FactorySignedAt);
        Assert.True(contract.IsFarmSigned);
        Assert.Equal(ContractStatus.Signed, contract.Status);
    }

    [Fact]
    public void ClearSignatures_ResetsPartyTimestamps()
    {
        var contract = new Contract
        {
            Status = ContractStatus.Signed,
            FactorySignedAt = DateTime.UtcNow,
            FarmSignedAt = DateTime.UtcNow,
            SignedAt = DateTime.UtcNow
        };

        contract.ClearSignatures();
        contract.Status = ContractStatus.Cancelled;

        Assert.Null(contract.FactorySignedAt);
        Assert.Null(contract.FarmSignedAt);
        Assert.Null(contract.SignedAt);
        Assert.False(contract.IsFactorySigned);
        Assert.False(contract.IsFarmSigned);
    }

    [Fact]
    public void RefreshSignatureStatus_DoesNotUncancel()
    {
        var contract = new Contract
        {
            Status = ContractStatus.Cancelled,
            FactorySignedAt = DateTime.UtcNow,
            FarmSignedAt = DateTime.UtcNow
        };

        contract.RefreshSignatureStatus();

        Assert.Equal(ContractStatus.Cancelled, contract.Status);
    }
}
