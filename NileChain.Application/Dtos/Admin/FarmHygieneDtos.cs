namespace NileChain.Application.Dtos.Admin;

public sealed class VerifyUserResult
{
    public bool Verified { get; set; }
    public bool KybIncomplete { get; set; }
    public List<string> MissingKybKinds { get; set; } = [];

    // Agent output (KYB doc vs RAG comparison)
    public int TrustScore { get; set; }
    public string OverallSummary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = "NeedsReview";
    public List<KybComparisonItemDto> Comparison { get; set; } = [];
}

public sealed class KybComparisonItemDto
{
    public string KybKind { get; set; } = string.Empty;
    public bool Provided { get; set; }
    public int KindTrustScore { get; set; }
    public string? RagExcerpt { get; set; }
    public List<string> Reasons { get; set; } = [];

    /// <summary>The signed contributions that add up to <see cref="KindTrustScore"/>.</summary>
    public List<KybScoreFactorDto> Factors { get; set; } = [];
}

public sealed class FarmHygieneDto
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool KybIncomplete { get; set; }
    public List<string> MissingKybKinds { get; set; } = [];
    public List<FarmHygieneDocumentDto> Documents { get; set; } = [];
    public List<FarmHygieneCertDto> Certifications { get; set; } = [];
}

public sealed class FarmHygieneDocumentDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string KybKind { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public sealed class FarmHygieneCertDto
{
    public Guid CertificationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AdminGranted { get; set; }
    public bool IsExpired { get; set; }
}

public sealed class GrantFarmCertificationRequest
{
    public Guid CertificationId { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
