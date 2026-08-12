using NileChain.Application.Common;
using NileChain.Application.Dtos.Integrity;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class ContractIntegrityService : IContractIntegrityService
{
    public const string OutcomeVerified = "Verified";
    public const string OutcomeSuperseded = "Superseded";
    public const string OutcomeTampered = "Tampered";
    public const string OutcomeNotFound = "NotFound";

    private readonly IContractIntegrityRepository _anchors;

    public ContractIntegrityService(IContractIntegrityRepository anchors) =>
        _anchors = anchors;

    public async Task AnchorIfFullySignedAsync(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.IsFullySigned)
            return;

        var payload = TryBuildPayload(contract);
        if (payload is null)
            return;

        var hash = ContractIntegrityHasher.ComputeContentHash(payload);
        var existing = await _anchors.GetActiveForContractAsync(contract.ContractId);
        if (existing is not null &&
            string.Equals(existing.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (existing is not null)
            await SupersedeActiveAsync(contract.ContractId);

        var head = await _anchors.GetChainHeadAsync();
        var nextIndex = (head?.ChainIndex ?? 0) + 1;
        var previous = head?.ContentHash ?? ContractIntegrityHasher.GenesisPreviousHash;

        var anchor = new ContractIntegrityAnchor
        {
            AnchorId = Guid.NewGuid(),
            ContractId = contract.ContractId,
            ContentHash = hash,
            PreviousHash = previous,
            ChainIndex = nextIndex,
            TxRef = ContractIntegrityHasher.BuildTxRef(nextIndex, hash),
            Status = ContractIntegrityAnchorStatus.Active,
            AnchoredAtUtc = DateTime.UtcNow
        };

        await _anchors.AddAsync(anchor);
        contract.IntegrityAnchors.Add(anchor);
    }

    public async Task SupersedeActiveAsync(Guid contractId)
    {
        var active = await _anchors.GetActiveAnchorsForContractAsync(contractId);
        foreach (var anchor in active)
        {
            anchor.Status = ContractIntegrityAnchorStatus.Superseded;
            _anchors.Update(anchor);
        }
    }

    public async Task<Result<ContractIntegrityDto>> GetActiveForContractAsync(Guid contractId)
    {
        var active = await _anchors.GetActiveForContractAsync(contractId);
        if (active is null)
            return Result<ContractIntegrityDto>.Failure(IntegrityErrors.NotAnchored);

        return Result<ContractIntegrityDto>.Success(MapDto(active));
    }

    public async Task<ContractIntegrityVerifyDto> VerifyByHashAsync(string contentHash)
    {
        var hash = (contentHash ?? string.Empty).Trim().ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            return new ContractIntegrityVerifyDto
            {
                Outcome = OutcomeNotFound,
                ContentHash = hash
            };
        }

        var anchor = await _anchors.GetByContentHashAsync(hash);
        if (anchor is null)
        {
            return new ContractIntegrityVerifyDto
            {
                Outcome = OutcomeNotFound,
                ContentHash = hash
            };
        }

        var contract = anchor.Contract;
        var currentMatches = CurrentContentMatches(contract, hash);
        var outcome = anchor.Status == ContractIntegrityAnchorStatus.Superseded
            ? OutcomeSuperseded
            : currentMatches
                ? OutcomeVerified
                : OutcomeTampered;

        var match = contract?.FarmMatch;
        return new ContractIntegrityVerifyDto
        {
            Outcome = outcome,
            ContentHash = anchor.ContentHash,
            PreviousHash = anchor.PreviousHash,
            ChainIndex = anchor.ChainIndex,
            TxRef = anchor.TxRef,
            AnchoredAtUtc = anchor.AnchoredAtUtc,
            Status = anchor.Status.ToString(),
            ContractId = anchor.ContractId,
            FarmName = match?.Farm?.Name,
            FactoryName = match?.SupplyRequest?.Factory?.Name,
            CropName = match?.SupplyRequest?.CropType?.Name,
            SignedAt = contract?.SignedAt,
            CurrentContentMatches = currentMatches
        };
    }

    public static ContractIntegrityDto? MapActive(Contract contract)
    {
        var active = contract.IntegrityAnchors?
            .Where(a => a.Status == ContractIntegrityAnchorStatus.Active)
            .OrderByDescending(a => a.ChainIndex)
            .FirstOrDefault();
        return active is null ? null : MapDto(active);
    }

    private static ContractIntegrityDto MapDto(ContractIntegrityAnchor a) => new()
    {
        AnchorId = a.AnchorId,
        ContractId = a.ContractId,
        ContentHash = a.ContentHash,
        ShortHash = ContractIntegrityHasher.ShortHash(a.ContentHash),
        PreviousHash = a.PreviousHash,
        ChainIndex = a.ChainIndex,
        TxRef = a.TxRef,
        Status = a.Status.ToString(),
        AnchoredAtUtc = a.AnchoredAtUtc
    };

    private static bool CurrentContentMatches(Contract? contract, string storedHash)
    {
        if (contract is null || !contract.IsFullySigned)
            return false;

        var payload = TryBuildPayload(contract);
        if (payload is null)
            return false;

        var computed = ContractIntegrityHasher.ComputeContentHash(payload);
        return string.Equals(computed, storedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static ContractIntegrityPayload? TryBuildPayload(Contract contract)
    {
        if (!contract.FarmSignedAt.HasValue || !contract.FactorySignedAt.HasValue)
            return null;

        var match = contract.FarmMatch;
        var qty = match is not null ? MatchCommercialTerms.QuantityTons(match) : 0m;
        var price = match is not null ? MatchCommercialTerms.PricePerTon(match) : null;
        if (price is null or <= 0)
            return null;

        return new ContractIntegrityPayload(
            contract.ContractId,
            match?.Farm?.Name ?? "",
            match?.SupplyRequest?.Factory?.Name ?? "",
            match?.SupplyRequest?.CropType?.Name ?? "",
            qty,
            price.Value,
            match is not null ? MatchCommercialTerms.DeliveryDate(match) : null,
            contract.GeneratedText ?? "",
            contract.FarmSignedAt.Value,
            contract.FactorySignedAt.Value);
    }
}
