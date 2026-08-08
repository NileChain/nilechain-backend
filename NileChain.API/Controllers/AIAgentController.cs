using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NileChain.AI.Models;
using NileChain.AI.Services;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.API.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = "Factory,Admin")]
public class AIAgentController : ControllerBase
{
    private readonly AIOrchestrationService _aiService;
    private readonly NileChainDbContext _db;

    public AIAgentController(AIOrchestrationService aiService, NileChainDbContext db)
    {
        _aiService = aiService;
        _db = db;
    }

    [HttpPost("run/{requestId:guid}")]
    public async Task<IActionResult> RunAgent(
        Guid requestId,
        [FromBody] AgentRequest request)
    {
        request.RequestId = requestId;
        var result = await _aiService.ProcessSupplyRequestAsync(request);

        // Always return the full AgentResponse (toolCallTrail, partialResult, mode)
        // even when Success=false — a bare string discarded live-test evidence.
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("generate-contract")]
    public async Task<IActionResult> GenerateContract(
        [FromBody] GenerateContractRequest request)
    {
        var result = await _aiService.GenerateContractAsync(
            request.AgentRequest,
            request.SelectedFarm,
            request.FactoryName);

        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { code = result.ErrorCode, message = result.ErrorMessage });
        }

        Guid? contractId = null;
        if (request.MatchId is Guid matchId && matchId != Guid.Empty)
        {
            var matchExists = await _db.FarmMatches.AnyAsync(m => m.MatchId == matchId);
            if (matchExists)
            {
                var existing = await _db.Contracts.FirstOrDefaultAsync(c => c.MatchId == matchId);
                if (existing is null)
                {
                    existing = new Contract
                    {
                        ContractId = Guid.NewGuid(),
                        MatchId = matchId,
                        GeneratedText = result.ContractText,
                        Status = ContractStatus.PendingSignature,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Contracts.Add(existing);
                }
                else
                {
                    existing.GeneratedText = result.ContractText;
                    if (existing.Status == ContractStatus.Cancelled)
                        existing.Status = ContractStatus.PendingSignature;
                }

                await _db.SaveChangesAsync();
                contractId = existing.ContractId;
            }
        }

        return Ok(new
        {
            contractText = result.ContractText,
            contractId,
            matchId = request.MatchId
        });
    }
}

public class GenerateContractRequest
{
    public AgentRequest AgentRequest { get; set; } = new();
    public MatchResult SelectedFarm { get; set; } = new();
    public string FactoryName { get; set; } = string.Empty;
    public Guid? MatchId { get; set; }
}
