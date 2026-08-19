using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

public static class ContractAuditHasher
{
    public const string GenesisPreviousHash =
        "0000000000000000000000000000000000000000000000000000000000000000";

    public static string Compute(
        string? previousStateHash,
        ContractAuditAction action,
        Guid actorId,
        DateTime timestamp,
        string? contractHash,
        string? ipAddress)
    {
        var prev = string.IsNullOrWhiteSpace(previousStateHash)
            ? GenesisPreviousHash
            : previousStateHash.Trim().ToLowerInvariant();

        var canonical = string.Join('|',
            prev,
            action.ToString(),
            actorId.ToString("N"),
            timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            (contractHash ?? "").Trim().ToLowerInvariant(),
            ipAddress ?? "");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
