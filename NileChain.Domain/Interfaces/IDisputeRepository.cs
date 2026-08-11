using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Interfaces;

public interface IDisputeRepository
{
    Task<Dispute?> GetByIdAsync(Guid disputeId, bool includeEvidence = true, bool includeEvents = true);
    Task<IReadOnlyList<Dispute>> GetByContractIdAsync(Guid contractId, bool includeEvidence = true, bool includeEvents = true);
    Task<bool> HasActiveDisputeAsync(Guid contractId);
    Task AddAsync(Dispute dispute);
    Task AddEvidenceAsync(DisputeEvidence evidence);
    Task AddEventAsync(DisputeEvent disputeEvent);

    /// <summary>
    /// Single-statement status update: succeeds only when current status equals <paramref name="expectedFrom"/>.
    /// </summary>
    Task<bool> TryAtomicTransitionAsync(
        Guid disputeId,
        DisputeStatus expectedFrom,
        DisputeStatus to,
        DateTime utcNow,
        string? adminNote,
        DisputeOutcomeFavor outcomeFavor,
        Guid actorUserId);

    Task<(IReadOnlyList<Dispute> Items, int TotalCount)> ListAdminAsync(
        DisputeStatus? status,
        DisputeType? type,
        int skip,
        int take);
}
