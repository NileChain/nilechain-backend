using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Signing;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class ContractHashService : IContractHashService
{
    private readonly SigningOptions _options;
    private readonly IContractSignatureRepository _signatures;

    public ContractHashService(
        IOptions<SigningOptions> options,
        IContractSignatureRepository signatures)
    {
        _options = options.Value;
        _signatures = signatures;
    }

    public string ComputeHash(Contract contract) =>
        ContractContentHasher.Compute(ContractContentHasher.FromContract(contract));

    public string ComputeSignatureToken(string hash, Guid userId, DateTime signedAt)
    {
        var secret = _options.HmacSecret;
        if (string.IsNullOrWhiteSpace(secret)
            || string.Equals(secret, "__SET_IN_LOCAL_CONFIG__", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Signing:HmacSecret is not configured.");
        }

        var payload = $"{hash}|{userId:N}|{signedAt.ToUniversalTime():o}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifySignatureToken(string hash, Guid userId, DateTime signedAt, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string expected;
        try
        {
            expected = ComputeSignatureToken(hash, userId, signedAt);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        byte[] a;
        byte[] b;
        try
        {
            a = Convert.FromHexString(token.Trim());
            b = Convert.FromHexString(expected);
        }
        catch (FormatException)
        {
            return false;
        }

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public async Task<Result<ContractVerificationDto>> VerifyAsync(
        Guid contractId,
        Guid requesterId,
        bool isAdmin)
    {
        var contract = await _signatures.GetContractWithPartiesAsync(contractId);
        if (contract is null)
            return Result<ContractVerificationDto>.Failure(SignatureErrors.ContractNotFound);

        if (!isAdmin && !IsParty(contract, requesterId))
            return Result<ContractVerificationDto>.Failure(SignatureErrors.Forbidden);

        var records = await _signatures.GetSignaturesForContractAsync(contractId);
        var audits = await _signatures.GetAuditTrailAsync(contractId);
        var currentHash = ComputeHash(contract);

        var tokensOk = records.Count > 0
            && records.All(r => VerifySignatureToken(r.ContractHash, r.SignerId, r.SignedAt, r.SignatureToken));
        var hashesMatch = records.Count > 0
            && records.All(r => string.Equals(r.ContractHash, currentHash, StringComparison.OrdinalIgnoreCase));

        var last = records.LastOrDefault();
        string? signerName = null;
        if (last is not null)
            signerName = ResolveSignerName(contract, last.SignerId);

        return Result<ContractVerificationDto>.Success(new ContractVerificationDto
        {
            IsValid = tokensOk && hashesMatch,
            SignedAt = last?.SignedAt,
            SignerName = signerName,
            ContractHash = last?.ContractHash ?? currentHash,
            HashMatchesCurrentContent = hashesMatch,
            AuditTrail = audits.Select(a => new ContractAuditTrailItemDto
            {
                Action = a.Action.ToString(),
                ActorId = a.ActorId,
                Timestamp = a.Timestamp,
                StateHash = a.StateHash
            }).ToList()
        });
    }

    private static bool IsParty(Contract contract, Guid userId)
    {
        var farmUser = contract.FarmMatch?.Farm?.UserId;
        var factoryUser = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        return farmUser == userId || factoryUser == userId;
    }

    private static string ResolveSignerName(Contract contract, Guid signerId)
    {
        if (contract.FarmMatch?.Farm?.UserId == signerId)
            return contract.FarmMatch.Farm.Name;
        if (contract.FarmMatch?.SupplyRequest?.Factory?.UserId == signerId)
            return contract.FarmMatch.SupplyRequest.Factory.Name;
        return signerId.ToString("N");
    }
}
