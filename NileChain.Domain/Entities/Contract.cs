using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

public class Contract
{
    public Guid ContractId { get; set; }
    public Guid MatchId { get; set; }
    public string? GeneratedText { get; set; }
    public string? PdfUrl { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set only when both parties have signed (fully executed).</summary>
    public DateTime? SignedAt { get; set; }

    /// <summary>Factory party signature timestamp. Null until factory explicitly signs.</summary>
    public DateTime? FactorySignedAt { get; set; }

    /// <summary>Farm party signature timestamp. Null until farm explicitly signs.</summary>
    public DateTime? FarmSignedAt { get; set; }

    /// <summary>When factory wallet funds for the full deal were moved Available → Held at full sign.</summary>
    public DateTime? FundsHeldAt { get; set; }

    /// <summary>
    /// Remaining deal hold (EGP) including platform fees. Set at full sign; reduced on
    /// release, QC refund, and unwind. <see cref="HasDealFundsHeld"/> stays true after
    /// remaining hits zero so later milestones do not debit Available again.
    /// </summary>
    public decimal? FundsHeldEgp { get; set; }

    public bool HasDealFundsHeld => FundsHeldAt.HasValue;

    public bool IsFactorySigned => FactorySignedAt.HasValue;
    public bool IsFarmSigned => FarmSignedAt.HasValue;
    public bool IsFullySigned => IsFactorySigned && IsFarmSigned;

    public FarmMatch FarmMatch { get; set; } = default!;
    public Fulfillment? Fulfillment { get; set; }
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
    public ICollection<RagDocument> RagDocuments { get; set; } = new List<RagDocument>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<ContractAttachment> Attachments { get; set; } = new List<ContractAttachment>();
    public ICollection<ContractIntegrityAnchor> IntegrityAnchors { get; set; } = new List<ContractIntegrityAnchor>();

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Recompute <see cref="Status"/> and <see cref="SignedAt"/> from independent party signatures.
    /// Does not clear Cancelled.
    /// </summary>
    public void RefreshSignatureStatus()
    {
        if (Status == ContractStatus.Cancelled)
            return;

        if (IsFactorySigned && IsFarmSigned)
        {
            Status = ContractStatus.Signed;
            var factoryAt = FactorySignedAt!.Value;
            var farmAt = FarmSignedAt!.Value;
            SignedAt = factoryAt >= farmAt ? factoryAt : farmAt;
            return;
        }

        SignedAt = null;

        if (IsFactorySigned)
        {
            Status = ContractStatus.PendingFarmSignature;
            return;
        }

        if (IsFarmSigned)
        {
            Status = ContractStatus.PendingFactorySignature;
            return;
        }

        // Neither party signed — keep Draft if still draft, otherwise awaiting first signature.
        if (Status != ContractStatus.Draft)
            Status = ContractStatus.PendingSignature;
    }

    public void ClearSignatures()
    {
        FactorySignedAt = null;
        FarmSignedAt = null;
        SignedAt = null;
    }
}
