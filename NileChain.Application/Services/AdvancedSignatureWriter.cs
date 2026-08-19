using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

/// <summary>Builds AES signature + audit rows. Caller persists them with the signature save.</summary>
public static class AdvancedSignatureWriter
{
    public static async Task AppendAsync(
        IContractSignatureRepository repo,
        IContractHashService hash,
        Contract contract,
        Guid signerId,
        DateTime signedAt,
        string? ipAddress,
        string? userAgent,
        string consentText,
        bool completingFullSign)
    {
        var contentHash = hash.ComputeHash(contract);
        var token = hash.ComputeSignatureToken(contentHash, signerId, signedAt);
        var previous = await repo.GetLatestAuditAsync(contract.ContractId);

        await repo.AddSignatureAsync(new ContractSignatureRecord
        {
            Id = Guid.NewGuid(),
            ContractId = contract.ContractId,
            SignerId = signerId,
            SignedAt = signedAt,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 512),
            ContractHash = contentHash,
            SignatureToken = token,
            ConsentText = consentText ?? string.Empty
        });

        var signedAtUtc = signedAt.Kind == DateTimeKind.Utc ? signedAt : signedAt.ToUniversalTime();
        var signedHash = ContractAuditHasher.Compute(
            previous?.StateHash,
            ContractAuditAction.Signed,
            signerId,
            signedAtUtc,
            contentHash,
            ipAddress);

        await repo.AddAuditAsync(new ContractAuditLog
        {
            Id = Guid.NewGuid(),
            ContractId = contract.ContractId,
            Action = ContractAuditAction.Signed,
            ActorId = signerId,
            IpAddress = Truncate(ipAddress, 45),
            Timestamp = signedAtUtc,
            StateHash = signedHash
        });

        if (!completingFullSign)
            return;

        var fullyAt = signedAtUtc;
        var fullyHash = ContractAuditHasher.Compute(
            signedHash,
            ContractAuditAction.FullySigned,
            signerId,
            fullyAt,
            contentHash,
            ipAddress);

        await repo.AddAuditAsync(new ContractAuditLog
        {
            Id = Guid.NewGuid(),
            ContractId = contract.ContractId,
            Action = ContractAuditAction.FullySigned,
            ActorId = signerId,
            IpAddress = Truncate(ipAddress, 45),
            Timestamp = fullyAt,
            StateHash = fullyHash
        });
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
    }
}
