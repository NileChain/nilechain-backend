using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class IntegrityErrors
{
    public static readonly Error ContractNotFound =
        new("Integrity.ContractNotFound", "Contract was not found.");

    public static readonly Error NotAnchored =
        new("Integrity.NotAnchored", "This contract has no integrity anchor yet.");

    public static readonly Error InvalidHash =
        new("Integrity.InvalidHash", "Provide a 64-character SHA-256 hex hash.");
}
