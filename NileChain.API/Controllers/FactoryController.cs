using NileChain.API.Extensions;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NileChain.Domain.Constants;

namespace NileChain.API.Controllers;

[Route("api/factory")]
[ApiController]
[Authorize(Roles = AppRoles.Factory)]
public class FactoryController : ControllerBase
{
    private readonly IFactoryService _factoryService;

    public FactoryController(IFactoryService factoryService)
    {
        _factoryService = factoryService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetProfileAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateFactoryProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.UpdateProfileAsync(Guid.Parse(userId), request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateSupplyRequestRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.CreateRequestAsync(Guid.Parse(userId), request);
        return result.ToActionResult();
    }

    [HttpGet("requests/{requestId:guid}/matches")]
    public async Task<IActionResult> GetRequestMatches(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetRequestMatchesAsync(Guid.Parse(userId), requestId);
        return result.ToActionResult();
    }

    [HttpGet("matched-farms")]
    public async Task<IActionResult> GetMatchedFarms()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetMatchedFarmsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetNotificationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(Guid notificationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.MarkNotificationAsReadAsync(Guid.Parse(userId), notificationId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetConversationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("conversations/{matchId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetMessagesAsync(Guid.Parse(userId), matchId);
        return result.ToActionResult();
    }

    [HttpPost("conversations/{matchId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid matchId, [FromBody] SendFactoryMessageRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.SendMessageAsync(Guid.Parse(userId), matchId, request.Content);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetContractsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}")]
    public async Task<IActionResult> GetContract(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetContractAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpPost("contracts")]
    public async Task<IActionResult> PersistContract([FromBody] PersistContractRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.PersistContractAsync(Guid.Parse(userId), request);
        return result.ToActionResult();
    }

    [HttpPut("contracts/{contractId:guid}/approve")]
    public async Task<IActionResult> ApproveContract(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.ApproveContractAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpPut("contracts/{contractId:guid}/reject")]
    public async Task<IActionResult> RejectContract(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.RejectContractAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/pdf")]
    public async Task<IActionResult> DownloadContractPdf(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetContractPdfAsync(Guid.Parse(userId), contractId);
        if (!result.IsSuccess)
            return result.ToActionResult();

        var (bytes, fileName) = result.Value!;
        return File(bytes, "application/pdf", fileName);
    }
}
