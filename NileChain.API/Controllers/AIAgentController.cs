using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.AI.Models;
using NileChain.AI.Services;

namespace NileChain.API.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize]
public class AIAgentController : ControllerBase
{
    private readonly AIOrchestrationService _aiService;

    public AIAgentController(AIOrchestrationService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("run/{requestId:guid}")]
    public async Task<IActionResult> RunAgent(
        Guid requestId,
        [FromBody] AgentRequest request)
    {
        request.RequestId = requestId;
        var result = await _aiService.ProcessSupplyRequestAsync(request);

        if (!result.Success)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result);
    }

    [HttpPost("generate-contract")]
    public async Task<IActionResult> GenerateContract(
        [FromBody] GenerateContractRequest request)
    {
        var contract = await _aiService.GenerateContractAsync(
            request.AgentRequest,
            request.SelectedFarm,
            request.FactoryName);

        return Ok(new { contractText = contract });
    }
}

public class GenerateContractRequest
{
    public AgentRequest AgentRequest { get; set; } = new();
    public MatchResult SelectedFarm { get; set; } = new();
    public string FactoryName { get; set; } = string.Empty;
}
