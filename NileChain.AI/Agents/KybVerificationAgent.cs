using System.Text.Json;
using NileChain.AI.RAG;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.AI.Agents;

/// <summary>
/// KYB review assistant: checks required kinds, filename heuristics, and whether
/// RAG has guidance for that kind. It does not OCR documents or extract national IDs.
/// </summary>
public sealed class KybVerificationAgent : IKybVerificationAgent
{
    private readonly ChromaService _chroma;
    private readonly IRepository<KybVerificationReport> _reports;
    private readonly IUnitOfWork _unitOfWork;

    // "Must-follow" threshold for admin gating (0..100).
    private const int DefaultTrustThreshold = 70;

    private const int UploadedBasePoints = 60;
    private const int SignalBonusPoints = 20;
    private const int MissingSignalPoints = 10;

    public KybVerificationAgent(
        ChromaService chroma,
        IRepository<KybVerificationReport> reports,
        IUnitOfWork unitOfWork)
    {
        _chroma = chroma;
        _reports = reports;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VerifyUserResult>> VerifyFarmAsync(
        Guid userId,
        Farm farm,
        CancellationToken cancellationToken = default)
    {
        return await VerifyAsync(
            userId,
            farm.Name,
            KybRequirements.RequiredForFarmVerifyWarning,
            (farm.FarmDocuments ?? Array.Empty<FarmDocument>())
                .Select(d => (d.KybKind, d.FileName)),
            cancellationToken);
    }

    public async Task<Result<VerifyUserResult>> VerifyFactoryAsync(
        Guid userId,
        Factory factory,
        CancellationToken cancellationToken = default)
    {
        return await VerifyAsync(
            userId,
            factory.Name,
            KybRequirements.RequiredForFactoryVerify,
            (factory.FactoryDocuments ?? Array.Empty<FactoryDocument>())
                .Select(d => (d.KybKind, d.FileName)),
            cancellationToken);
    }

    private async Task<Result<VerifyUserResult>> VerifyAsync(
        Guid userId,
        string? entityName,
        IReadOnlyList<KybKind> requiredKinds,
        IEnumerable<(KybKind kind, string fileName)> documents,
        CancellationToken cancellationToken = default)
    {
        var providedByKind = documents
            .GroupBy(d => d.kind)
            .ToDictionary(g => g.Key, g => g.Select(x => x.fileName).ToList());

        var perKind = new List<KybComparisonItemDto>();
        var missingKinds = new List<string>();

        var trustSum = 0;

        foreach (var kind in requiredKinds)
        {
            var provided = providedByKind.TryGetValue(kind, out var list) && list.Count > 0;
            if (!provided)
                missingKinds.Add(kind.ToString());

            var fileName = provided ? list![0] : null;

            var ragQuery = BuildRagQuery(kind);
            // Chroma query is executed by the proxy; this repo's current ChromaService
            // doesn't support cancellation tokens.
            var rag = await _chroma.QueryAsync(ragQuery, nResults: 2);
            var ragExcerpt = rag.IsAvailable && !string.IsNullOrWhiteSpace(rag.Content)
                ? rag.Content.Length > 700 ? rag.Content[..700] + "…" : rag.Content
                : null;

            var (kindTrustScore, factors) = CalculateKindTrustScore(kind, provided, fileName, ragExcerpt);

            perKind.Add(new KybComparisonItemDto
            {
                KybKind = kind.ToString(),
                Provided = provided,
                KindTrustScore = kindTrustScore,
                RagExcerpt = ragExcerpt,
                Factors = factors,
                Reasons = factors.Select(KybScoreFactorCodes.Describe).ToList()
            });

            trustSum += kindTrustScore;
        }

        var trustScore = (int)Math.Round((double)trustSum / requiredKinds.Count, MidpointRounding.AwayFromZero);
        var recommendation = Recommend(missingKinds.Count, requiredKinds.Count, trustScore);
        var overallSummary = BuildOverallSummary(entityName, missingKinds, trustScore, recommendation);

        // Persist report for audit/debug (admin can inspect later if UI grows).
        var report = new KybVerificationReport
        {
            ReportId = Guid.NewGuid(),
            UserId = userId,
            TrustScore = trustScore,
            Recommendation = recommendation,
            OverallSummary = overallSummary,
            BreakdownJson = JsonSerializer.Serialize(perKind, new JsonSerializerOptions
            {
                WriteIndented = false
            }),
            CreatedAt = DateTime.UtcNow
        };

        await _reports.AddAsync(report);
        await _unitOfWork.SaveChangesAsync();

        // Assist-only: admin still decides. Verified stays false here.
        return Result<VerifyUserResult>.Success(new VerifyUserResult
        {
            Verified = false,
            KybIncomplete = missingKinds.Count > 0,
            MissingKybKinds = missingKinds,
            TrustScore = trustScore,
            OverallSummary = overallSummary,
            Recommendation = recommendation,
            Comparison = perKind
        });
    }

    private static string BuildRagQuery(KybKind kind) => kind switch
    {
        KybKind.CommercialRegister => "متطلبات السجل التجاري للمنشآت الزراعية",
        KybKind.TaxCard => "متطلبات بطاقة ضريبة الدخل للمزارع والشركات",
        KybKind.NationalId => "متطلبات إثبات الهوية/الرقم القومي للملاك",
        KybKind.LandLease => "متطلبات عقود/إثبات ملكية أو إيجار الأرض الزراعية",
        _ => "متطلبات مستندات KYB العامة",
    };

    /// <summary>
    /// Unchanged arithmetic (60 base, ±20/−10 for the two heuristics), but expressed as the
    /// factors the admin screen shows, so the explanation and the score share one source.
    /// </summary>
    private static (int kindTrustScore, List<KybScoreFactorDto> factors) CalculateKindTrustScore(
        KybKind kind,
        bool provided,
        string? fileName,
        string? ragExcerpt)
    {
        if (!provided)
            return (0, [Factor(KybScoreFactorCodes.DocumentMissing, 0)]);

        var factors = new List<KybScoreFactorDto>
        {
            Factor(KybScoreFactorCodes.DocumentUploaded, UploadedBasePoints)
        };

        var fileNameLower = fileName?.ToLowerInvariant() ?? string.Empty;
        var keywordHits = kind switch
        {
            KybKind.CommercialRegister => ContainsAny(fileNameLower, ["commercial", "register", "سجل", "تجاري", "نموذج", "سجل-"]),
            KybKind.TaxCard => ContainsAny(fileNameLower, ["tax", "taxcard", "ضريبة", "ضرائب", "بطاقة-ضريبية", "مصلحة"]),
            KybKind.NationalId => ContainsAny(fileNameLower, ["national", "id", "رقم", "قومي", "هوية", "بطاقة", "ident"]),
            KybKind.LandLease => ContainsAny(fileNameLower, ["lease", "land", "عقد", "إيجار", "ملكية", "أرض"]),
            _ => false
        };

        factors.Add(keywordHits
            ? Factor(KybScoreFactorCodes.FileNameMatched, SignalBonusPoints)
            : Factor(KybScoreFactorCodes.FileNameUnmatched, -MissingSignalPoints));

        factors.Add(!string.IsNullOrWhiteSpace(ragExcerpt)
            ? Factor(KybScoreFactorCodes.GuidanceAvailable, SignalBonusPoints)
            : Factor(KybScoreFactorCodes.GuidanceUnavailable, -MissingSignalPoints));

        var score = Math.Clamp(factors.Sum(f => f.Delta), 0, 100);

        return (score, factors);
    }

    private static KybScoreFactorDto Factor(string code, int delta) =>
        new() { Code = code, Delta = delta };

    private static bool ContainsAny(string text, string[] needles)
    {
        foreach (var n in needles)
        {
            if (string.IsNullOrWhiteSpace(n)) continue;
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string Recommend(int missingCount, int requiredCount, int trustScore)
    {
        if (missingCount == requiredCount)
            return nameof(KybRecommendation.Reject);
        if (missingCount > 0)
            return nameof(KybRecommendation.NeedsReview);
        if (trustScore >= DefaultTrustThreshold)
            return nameof(KybRecommendation.Approve);
        if (trustScore < 40)
            return nameof(KybRecommendation.Reject);
        return nameof(KybRecommendation.NeedsReview);
    }

    private static string BuildOverallSummary(
        string? entityName,
        List<string> missingKinds,
        int trustScore,
        string recommendation)
    {
        var displayName = string.IsNullOrWhiteSpace(entityName) ? "Business" : entityName;
        var missing = missingKinds.Count > 0
            ? $"Missing: {string.Join(", ", missingKinds)}"
            : "All required KYB kinds provided.";
        return $"KYB review assist for {displayName}: {trustScore}/100, recommend {recommendation}. {missing} This is a checklist assist, not document OCR.";
    }
}

