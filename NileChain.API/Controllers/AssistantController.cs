using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.AI.Models;
using NileChain.AI.Services;
using NileChain.Domain.Constants;

namespace NileChain.API.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize(Roles = $"{AppRoles.Farm},{AppRoles.Factory},{AppRoles.Admin},{AppRoles.SuperAdmin}")]
public class AssistantController : ControllerBase
{
    private readonly CopilotChatService _copilot;

    public AssistantController(CopilotChatService copilot) => _copilot = copilot;

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] CopilotChatRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var result = await _copilot.ChatAsync(Guid.Parse(userId), roles, request, cancellationToken);
        if (!result.Success)
        {
            var status = string.Equals(
                result.ErrorCode,
                NileChain.Application.Common.ClientErrorSanitizer.ServiceUnavailableCode,
                StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status400BadRequest;
            return StatusCode(status, result);
        }

        return Ok(result);
    }
}
