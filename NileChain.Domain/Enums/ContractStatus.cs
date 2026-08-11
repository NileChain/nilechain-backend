namespace NileChain.Domain.Enums;

public enum ContractStatus
{
    Draft,
    /// <summary>Generated; neither party has signed yet.</summary>
    PendingSignature,
    /// <summary>Factory signed; waiting for farm.</summary>
    PendingFarmSignature,
    /// <summary>Farm signed; waiting for factory.</summary>
    PendingFactorySignature,
    /// <summary>Both parties signed.</summary>
    Signed,
    Cancelled
}
