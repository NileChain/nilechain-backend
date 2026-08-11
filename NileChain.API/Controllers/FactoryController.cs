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
    private readonly IFulfillmentService _fulfillmentService;
    private readonly IPaymentMilestoneService _paymentMilestoneService;
    private readonly IDisputeService _disputeService;

    public FactoryController(
        IFactoryService factoryService,
        IFulfillmentService fulfillmentService,
        IPaymentMilestoneService paymentMilestoneService,
        IDisputeService disputeService)
    {
        _factoryService = factoryService;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
        _disputeService = disputeService;
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

        var headerKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await _factoryService.CreateRequestAsync(
            Guid.Parse(userId),
            request,
            headerKey);
        return result.ToActionResult();
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetRequestsAsync(Guid.Parse(userId), page, pageSize);
        return result.ToActionResult();
    }

    [HttpGet("requests/{requestId:guid}/matches")]
    public async Task<IActionResult> GetRequestMatches(Guid requestId, [FromQuery] string? sort)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetRequestMatchesAsync(Guid.Parse(userId), requestId, sort);
        return result.ToActionResult();
    }

    [HttpPost("matches/{matchId:guid}/exclude")]
    public async Task<IActionResult> ExcludeMatch(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.ExcludeMatchAsync(Guid.Parse(userId), matchId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
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

    [HttpGet("contracts/{contractId:guid}/fulfillment")]
    public async Task<IActionResult> GetFulfillment(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.GetByContractAsync(
            Guid.Parse(userId), contractId, asFarm: false);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/fulfillment/receive")]
    public async Task<IActionResult> MarkReceived(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.MarkReceivedAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/fulfillment/quality-check")]
    public async Task<IActionResult> MarkQualityChecked(
        Guid contractId,
        [FromBody] NileChain.Application.Dtos.Fulfillment.QualityCheckRequest? request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.MarkQualityCheckedAsync(
            Guid.Parse(userId), contractId, request?.Notes);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/fulfillment/fulfill")]
    public async Task<IActionResult> MarkFulfilled(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.MarkFulfilledAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/payment-milestones")]
    public async Task<IActionResult> GetPaymentMilestones(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _paymentMilestoneService.GetByContractAsync(
            Guid.Parse(userId), contractId, asFarm: false);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/payment-milestones/{transactionId:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaymentMilestonePaid(Guid contractId, Guid transactionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _paymentMilestoneService.MarkPaidAsync(
            Guid.Parse(userId), contractId, transactionId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/disputes")]
    public async Task<IActionResult> ListDisputes(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _disputeService.ListForContractAsync(
            Guid.Parse(userId), contractId, asFarm: false);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/disputes")]
    [RequestSizeLimit(NileChain.Application.Validation.FileUploadValidation.MaxBytes * 5)]
    public async Task<IActionResult> OpenDispute(
        Guid contractId,
        [FromForm] string type,
        [FromForm] string description,
        [FromForm] List<IFormFile>? evidence)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _disputeService.OpenAsync(
            Guid.Parse(userId),
            contractId,
            asFarm: false,
            type,
            description,
            evidence);
        return result.ToActionResult();
    }

    [HttpGet("disputes/{disputeId:guid}")]
    public async Task<IActionResult> GetDispute(Guid disputeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _disputeService.GetAsync(Guid.Parse(userId), disputeId, asFarm: false);
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
