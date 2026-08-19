using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.AI.RAG;
using NileChain.AI.Sbg;
using NileChain.Application.Common;
using NileChain.Domain.Constants;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Services;

public sealed class CopilotChatService
{
    private readonly OpenAiKernelProvider _kernelProvider;
    private readonly SbgStudentChatClient _sbgClient;
    private readonly RagPipeline _rag;
    private readonly NileChainDbContext _db;

    public CopilotChatService(
        OpenAiKernelProvider kernelProvider,
        SbgStudentChatClient sbgClient,
        RagPipeline rag,
        NileChainDbContext db)
    {
        _kernelProvider = kernelProvider;
        _sbgClient = sbgClient;
        _rag = rag;
        _db = db;
    }

    public async Task<CopilotChatResponse> ChatAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CopilotChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = (request.Message ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return Fail("Message is required.");
        }

        if (!_kernelProvider.IsAvailable && !_sbgClient.IsConfigured)
        {
            return new CopilotChatResponse
            {
                Success = false,
                Reply = ClientErrorSanitizer.ServiceUnavailableMessage,
                ErrorCode = ClientErrorSanitizer.ServiceUnavailableCode
            };
        }

        var context = await BuildDealContextAsync(userId, roles, request, cancellationToken);
        var cropForRag = context.CropType ?? "agricultural supply";
        var rag = await _rag.GetCombinedContextAsync(cropForRag);

        // With no deal in scope and no retrieved passage there is nothing to ground an answer on,
        // so say that instead of spending a call and letting the model answer from memory.
        if (!context.HasDeal && !rag.HasKnowledge)
        {
            return new CopilotChatResponse
            {
                Success = true,
                Reply = NoGroundingReply,
                UsedRag = false,
                KnowledgeUnavailable = true
            };
        }

        var ragText = rag.HasKnowledge
            ? rag.CitedText
            : "(no knowledge-base passage matched this question)";

        var system = $"""
            You are NileChain Copilot for Egyptian B2B farm↔factory deals.
            Answer only from the DEAL CONTEXT and the numbered SOURCES below.
            Do not invent farms, prices, contracts, or standards.
            Cite every claim taken from a SOURCE with its number in square brackets, e.g. [1].
            Never cite a number that is not listed in SOURCES, and never cite the DEAL CONTEXT.
            {(rag.HasKnowledge
                ? string.Empty
                : "SOURCES is empty: state that the knowledge base has nothing on this and answer only from DEAL CONTEXT.")}
            If context is missing, say so and suggest the user open a match or contract.
            Reply in the same language as the user (Arabic or English). Be concise.
            """;

        var userPrompt =
            $"ROLE: {string.Join(',', roles)}\nPROMPT_ID: {request.PromptId ?? "free-text"}\n\n" +
            $"DEAL CONTEXT:\n{context.Text}\n\nSOURCES:\n{ragText}\n\nUSER:\n{message}";

        try
        {
            string reply;
            string provider;
            if (_kernelProvider.Kernel is { } kernel)
            {
                var result = await kernel.InvokePromptAsync(
                    $"{system}\n\n{userPrompt}",
                    cancellationToken: cancellationToken);
                reply = result.GetValue<string>() ?? string.Empty;
                provider = _kernelProvider.ProviderName;
            }
            else
            {
                reply = await _sbgClient.ChatAsync(
                    system,
                    [new SbgChatMessage { Role = "user", Content = userPrompt }],
                    maxTokens: 500,
                    cancellationToken: cancellationToken);
                provider = "SBG";
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                return new CopilotChatResponse
                {
                    Success = false,
                    Reply = ClientErrorSanitizer.ServiceUnavailableMessage,
                    ErrorCode = ClientErrorSanitizer.ServiceUnavailableCode
                };
            }

            // Markers pointing at sources that were never retrieved are stripped, so the UI
            // can only ever show a citation that maps to a real passage.
            var (answer, used) = rag.ResolveCitations(reply);

            return new CopilotChatResponse
            {
                Success = true,
                Reply = answer,
                UsedRag = used.Count > 0,
                KnowledgeUnavailable = !rag.HasKnowledge,
                Provider = provider,
                Citations = used
                    .Select(c => new CopilotCitation
                    {
                        Index = c.Index,
                        Id = c.Id,
                        Section = c.Section,
                        Title = c.Title,
                        Excerpt = c.Excerpt
                    })
                    .ToList()
            };
        }
        catch (Exception)
        {
            return new CopilotChatResponse
            {
                Success = false,
                Reply = ClientErrorSanitizer.ServiceUnavailableMessage,
                ErrorCode = ClientErrorSanitizer.ServiceUnavailableCode
            };
        }
    }

    internal const string NoGroundingReply =
        "لا توجد صفقة محدَّدة ولا مصدر في قاعدة المعرفة يخصّ هذا السؤال، "
        + "لذلك لا أستطيع الإجابة بمصدر موثوق. افتح مطابقة أو عقداً، أو اسأل عن بيانات صفقة قائمة.";

    private async Task<(string Text, string? CropType, bool HasDeal)> BuildDealContextAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CopilotChatRequest request,
        CancellationToken cancellationToken)
    {
        var isAdmin = roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.SuperAdmin);
        var isFarm = roles.Contains(AppRoles.Farm);
        var isFactory = roles.Contains(AppRoles.Factory);
        var lines = new List<string>();
        string? crop = null;

        if (request.RequestId is Guid rid && rid != Guid.Empty)
        {
            var supply = await _db.SupplyRequests
                .AsNoTracking()
                .Include(r => r.CropType)
                .Include(r => r.Factory)
                .FirstOrDefaultAsync(r => r.RequestId == rid, cancellationToken);
            if (supply is not null &&
                (isAdmin || (isFactory && supply.Factory.UserId == userId)))
            {
                crop = supply.CropType?.Name;
                lines.Add(
                    $"SupplyRequest {supply.RequestId:N}: crop={crop}, qty={supply.QuantityTons}t, " +
                    $"price={supply.PricePerTon} EGP/t, delivery={supply.DeliveryDate:yyyy-MM-dd}, " +
                    $"gov={supply.Factory.Governorate}, status={supply.Status}, specs={Trim(supply.QualitySpecs, 240)}");
            }
        }

        if (request.MatchId is Guid mid && mid != Guid.Empty)
        {
            var match = await _db.FarmMatches
                .AsNoTracking()
                .Include(m => m.Farm)
                .Include(m => m.SupplyRequest).ThenInclude(r => r.CropType)
                .Include(m => m.SupplyRequest).ThenInclude(r => r.Factory)
                .FirstOrDefaultAsync(m => m.MatchId == mid, cancellationToken);
            if (match is not null &&
                (isAdmin
                 || (isFarm && match.Farm.UserId == userId)
                 || (isFactory && match.SupplyRequest.Factory.UserId == userId)))
            {
                crop ??= match.SupplyRequest.CropType?.Name;
                lines.Add(
                    $"Match {match.MatchId:N}: farm={match.Farm.Name} ({match.Farm.Governorate}), " +
                    $"status={match.Status}, matchScore={match.MatchScore}, riskScore={match.RiskScore}, " +
                    $"eligibility={Trim(match.EligibilitySnapshotJson, 280)}");
            }
        }

        if (request.ContractId is Guid cid && cid != Guid.Empty)
        {
            var contract = await _db.Contracts
                .AsNoTracking()
                .Include(c => c.FarmMatch).ThenInclude(m => m.Farm)
                .Include(c => c.FarmMatch).ThenInclude(m => m.SupplyRequest).ThenInclude(r => r.Factory)
                .FirstOrDefaultAsync(c => c.ContractId == cid, cancellationToken);
            if (contract is not null &&
                (isAdmin
                 || (isFarm && contract.FarmMatch.Farm.UserId == userId)
                 || (isFactory && contract.FarmMatch.SupplyRequest.Factory.UserId == userId)))
            {
                lines.Add(
                    $"Contract {contract.ContractId:N}: status={contract.Status}, " +
                    $"farmSigned={contract.IsFarmSigned}, factorySigned={contract.IsFactorySigned}, " +
                    $"excerpt={Trim(contract.GeneratedText, 400)}");
            }
        }

        var hasDeal = lines.Count > 0;
        if (!hasDeal)
            lines.Add("No in-scope deal is selected. Ask a general NileChain question or open a match/contract.");

        return (string.Join('\n', lines), crop, hasDeal);
    }

    private static CopilotChatResponse Fail(string message) =>
        new()
        {
            Success = false,
            Reply = message,
            ErrorCode = "AI.InvalidRequest"
        };

    private static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var t = value.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }
}
