using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NileChain.AI.Models;
using NileChain.AI.Services;
using NileChain.Application.Common;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.API.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = "Factory,Admin,SuperAdmin")]
public class AIAgentController : ControllerBase
{
    private readonly AIOrchestrationService _aiService;
    private readonly NileChainDbContext _db;
    private readonly ILogger<AIAgentController> _logger;
    private readonly NileChain.Application.Interfaces.IFulfillmentService _fulfillmentService;
    private readonly NileChain.Application.Interfaces.IPaymentMilestoneService _paymentMilestoneService;
    private readonly NileChain.Application.Interfaces.IDisputeService _disputeService;
    private readonly NileChain.Application.Interfaces.IContractIntegrityService _integrity;

    public AIAgentController(
        AIOrchestrationService aiService,
        NileChainDbContext db,
        ILogger<AIAgentController> logger,
        NileChain.Application.Interfaces.IFulfillmentService fulfillmentService,
        NileChain.Application.Interfaces.IPaymentMilestoneService paymentMilestoneService,
        NileChain.Application.Interfaces.IDisputeService disputeService,
        NileChain.Application.Interfaces.IContractIntegrityService integrity)
    {
        _aiService = aiService;
        _db = db;
        _logger = logger;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
        _disputeService = disputeService;
        _integrity = integrity;
    }

    [HttpPost("run/{requestId:guid}")]
    public async Task<IActionResult> RunAgent(
        Guid requestId,
        [FromBody] AgentRequest request)
    {
        var ownership = await EnsureRequestOwnershipAsync(requestId);
        if (ownership is not null)
            return ownership;

        request.RequestId = requestId;
        var result = await _aiService.ProcessSupplyRequestAsync(request);

        // Always return the full AgentResponse (toolCallTrail, partialResult, mode)
        // even when Success=false — a bare string discarded live-test evidence.
        if (!result.Success)
        {
            result.ErrorCode ??= ClientErrorSanitizer.AgentFailureCode;
            result.ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? ClientErrorSanitizer.AgentFailureMessage
                : ClientErrorSanitizer.SanitizeTrailText(result.ErrorMessage);
            result.ToolCallTrail = result.ToolCallTrail
                .Select(t => new ToolCallTrailEntry
                {
                    TimestampUtc = t.TimestampUtc,
                    FunctionName = t.FunctionName,
                    ArgumentsSummary = ClientErrorSanitizer.SanitizeTrailText(t.ArgumentsSummary),
                    ResultSummary = ClientErrorSanitizer.SanitizeTrailText(t.ResultSummary),
                    Blocked = t.Blocked,
                    BlockReason = t.BlockReason is null
                        ? null
                        : ClientErrorSanitizer.SanitizeTrailText(t.BlockReason)
                }).ToList();

            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("history")]
    [HttpGet("runs")]
    public async Task<IActionResult> GetAgentHistory(
        [FromQuery] Guid? requestId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AgentRuns.AsNoTracking().AsQueryable();

        if (!IsElevatedAdmin())
        {
            var factoryId = await ResolveCallerFactoryIdAsync();
            if (factoryId is null)
                return Forbid();

            query = query.Where(r => r.FactoryId == factoryId.Value);
        }

        if (requestId is Guid rid && rid != Guid.Empty)
        {
            var ownership = await EnsureRequestOwnershipAsync(rid);
            if (ownership is not null)
                return ownership;

            query = query.Where(r => r.RequestId == rid);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.RunId,
                r.RequestId,
                r.FactoryId,
                r.StartedAt,
                r.CompletedAt,
                r.Success,
                r.ErrorCode,
                r.TruncatedCount,
                r.OrchestratorMode
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            totalCount = total,
            page,
            pageSize
        });
    }

    [HttpPost("generate-contract")]
    public async Task<IActionResult> GenerateContract(
        [FromBody] GenerateContractRequest request)
    {
        if (request.MatchId is Guid matchId && matchId != Guid.Empty)
        {
            var matchOwnership = await EnsureMatchOwnershipAsync(matchId);
            if (matchOwnership is not null)
                return matchOwnership;
        }
        else if (request.AgentRequest?.RequestId is Guid reqId && reqId != Guid.Empty)
        {
            var requestOwnership = await EnsureRequestOwnershipAsync(reqId);
            if (requestOwnership is not null)
                return requestOwnership;
        }

        ContractGenerationResult result;
        try
        {
            result = await _aiService.GenerateContractAsync(
                request.AgentRequest ?? new AgentRequest(),
                request.SelectedFarm ?? new MatchResult(),
                request.FactoryName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateContract failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    code = ClientErrorSanitizer.ServiceUnavailableCode,
                    message = ClientErrorSanitizer.ServiceUnavailableMessage
                });
        }

        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    code = result.ErrorCode ?? ClientErrorSanitizer.ServiceUnavailableCode,
                    message = ClientErrorSanitizer.SanitizeTrailText(result.ErrorMessage)
                               is { Length: > 0 } msg
                        ? msg
                        : ClientErrorSanitizer.ServiceUnavailableMessage
                });
        }

        Guid? contractId = null;
        if (request.MatchId is Guid persistMatchId && persistMatchId != Guid.Empty)
        {
            var match = await _db.FarmMatches
                .Include(m => m.Contract)
                .Include(m => m.Farm)
                .Include(m => m.SupplyRequest)
                    .ThenInclude(r => r.Factory)
                .FirstOrDefaultAsync(m => m.MatchId == persistMatchId);

            if (match is not null)
            {
                var notify = false;
                var voidFulfillment = false;
                if (match.Contract is null)
                {
                    if (!ContractExecution.CanCreateContract(match))
                    {
                        return Conflict(new
                        {
                            code = "Factory.MatchNotProposed",
                            message = "Contracts can only be created while the match is Proposed."
                        });
                    }

                    var created = new Contract
                    {
                        ContractId = Guid.NewGuid(),
                        MatchId = persistMatchId,
                        GeneratedText = ContractSignatureText.StripHandwrittenBlocks(result.ContractText),
                        Status = ContractStatus.PendingSignature,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Contracts.Add(created);
                    contractId = created.ContractId;
                    notify = true;
                }
                else
                {
                    var textChanged = !string.Equals(
                        match.Contract.GeneratedText,
                        result.ContractText,
                        StringComparison.Ordinal);

                    if (textChanged)
                    {
                        // Regenerating text clears signatures; Accepted → Proposed atomically.
                        // Active disputes block regen — same policy as FactoryService.PersistContract.
                        if (await _disputeService.HasActiveDisputeAsync(match.Contract.ContractId))
                        {
                            return Conflict(new
                            {
                                code = "Dispute.RegenBlocked",
                                message = "Contract text cannot be regenerated while a dispute is open or under review."
                            });
                        }

                        if (!ContractExecution.TryReplaceGeneratedText(match.Contract, match, result.ContractText))
                        {
                            return Conflict(new
                            {
                                code = "Factory.MatchNotProposed",
                                message = "Contracts can only be updated while the match is Proposed (or Accepted for regen)."
                            });
                        }

                        await _integrity.SupersedeActiveAsync(match.Contract.ContractId);
                        notify = true;
                        voidFulfillment = true;
                    }

                    contractId = match.Contract.ContractId;
                }

                if (notify)
                {
                    var farmUserId = match.Farm?.UserId ?? Guid.Empty;
                    var factoryName = match.SupplyRequest?.Factory?.Name ?? "Factory";
                    if (farmUserId != Guid.Empty)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = farmUserId,
                            Title = "Contract ready for signature",
                            Message = $"A supply contract from {factoryName} is ready for review.",
                            Type = "ContractReady",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Conflict(new
                    {
                        code = "Factory.ConcurrencyConflict",
                        message = "The contract was modified by another request. Refresh and try again."
                    });
                }

                if (voidFulfillment && contractId is Guid voidId)
                {
                    var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (Guid.TryParse(actor, out var actorId))
                    {
                        await _fulfillmentService.VoidForContractAsync(
                            voidId,
                            actorId,
                            "Contract text regenerated via agent — prior fulfillment voided");
                        await _paymentMilestoneService.VoidForContractAsync(
                            voidId,
                            actorId,
                            "Contract text regenerated via agent — prior payment milestone schedule voided");
                    }
                }
            }
        }

        return Ok(new
        {
            contractText = result.ContractText,
            contractId,
            matchId = request.MatchId
        });
    }

    private async Task<IActionResult?> EnsureRequestOwnershipAsync(Guid requestId)
    {
        if (IsElevatedAdmin())
            return null;

        var factoryId = await ResolveCallerFactoryIdAsync();
        if (factoryId is null)
            return OwnershipForbidden();

        var owns = await _db.SupplyRequests.AnyAsync(r =>
            r.RequestId == requestId && r.FactoryId == factoryId.Value);
        return owns ? null : OwnershipForbidden();
    }

    private async Task<IActionResult?> EnsureMatchOwnershipAsync(Guid matchId)
    {
        if (IsElevatedAdmin())
            return null;

        var factoryId = await ResolveCallerFactoryIdAsync();
        if (factoryId is null)
            return OwnershipForbidden();

        var owns = await _db.FarmMatches.AnyAsync(m =>
            m.MatchId == matchId && m.SupplyRequest.FactoryId == factoryId.Value);
        return owns ? null : OwnershipForbidden();
    }

    private static IActionResult OwnershipForbidden() =>
        new ObjectResult(new
        {
            code = "Factory.Forbidden",
            message = "You do not have access to generate a contract for this match or request."
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };

    private bool IsElevatedAdmin() =>
        User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    private async Task<Guid?> ResolveCallerFactoryIdAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return null;

        var factory = await _db.Factory.AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId);
        return factory?.FactoryId;
    }
}

public class GenerateContractRequest
{
    public AgentRequest AgentRequest { get; set; } = new();
    public MatchResult SelectedFarm { get; set; } = new();
    public string FactoryName { get; set; } = string.Empty;
    public Guid? MatchId { get; set; }
}
