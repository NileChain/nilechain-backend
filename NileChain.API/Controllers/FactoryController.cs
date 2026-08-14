using NileChain.API.Extensions;
using NileChain.Application.Dtos.Contracts;
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
    private readonly IMockEscrowPaymentService _mockEscrowPaymentService;
    private readonly IDisputeService _disputeService;
    private readonly IContractAttachmentService _attachmentService;
    private readonly IContractDateAmendmentService _dateAmendmentService;
    private readonly IContractChangeRequestService _changeRequestService;

    public FactoryController(
        IFactoryService factoryService,
        IFulfillmentService fulfillmentService,
        IPaymentMilestoneService paymentMilestoneService,
        IMockEscrowPaymentService mockEscrowPaymentService,
        IDisputeService disputeService,
        IContractAttachmentService attachmentService,
        IContractDateAmendmentService dateAmendmentService,
        IContractChangeRequestService changeRequestService)
    {
        _factoryService = factoryService;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
        _mockEscrowPaymentService = mockEscrowPaymentService;
        _disputeService = disputeService;
        _attachmentService = attachmentService;
        _dateAmendmentService = dateAmendmentService;
        _changeRequestService = changeRequestService;
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

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetDashboardAsync(Guid.Parse(userId));
        return result.ToActionResult();
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
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetRequestsAsync(
            Guid.Parse(userId), page, pageSize, status);
        return result.ToActionResult();
    }

    [HttpGet("requests/{requestId:guid}")]
    public async Task<IActionResult> GetRequest(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetRequestAsync(Guid.Parse(userId), requestId);
        return result.ToActionResult();
    }

    [HttpPut("requests/{requestId:guid}/delivery-terms")]
    public async Task<IActionResult> UpdateRequestDeliveryTerms(
        Guid requestId,
        [FromBody] UpdateSupplyRequestDeliveryTermsRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.UpdateRequestDeliveryTermsAsync(
            Guid.Parse(userId), requestId, request);
        return result.ToActionResult();
    }

    [HttpPost("requests/{requestId:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.CancelRequestAsync(Guid.Parse(userId), requestId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("suppliers/{farmId:guid}/scorecard")]
    public async Task<IActionResult> GetSupplierScorecard(Guid farmId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetSupplierScorecardAsync(Guid.Parse(userId), farmId);
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

    [HttpPost("matches/{matchId:guid}/accept-counter")]
    public async Task<IActionResult> AcceptCounter(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.AcceptCounterOfferAsync(Guid.Parse(userId), matchId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("matches/{matchId:guid}/reject-counter")]
    public async Task<IActionResult> RejectCounter(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.RejectCounterOfferAsync(Guid.Parse(userId), matchId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("listings")]
    public async Task<IActionResult> GetListings(
        [FromQuery] Guid? cropTypeId,
        [FromQuery] string? governorate)
    {
        var result = await _factoryService.GetPublishedListingsAsync(cropTypeId, governorate);
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

    [HttpGet("farms/{farmId:guid}/active-match")]
    public async Task<IActionResult> GetActiveMatchWithFarm(Guid farmId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetActiveMatchWithFarmAsync(Guid.Parse(userId), farmId);
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
    public async Task<IActionResult> MarkReceived(
        Guid contractId,
        [FromBody] NileChain.Application.Dtos.Fulfillment.ReceiveFulfillmentRequest? request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.MarkReceivedAsync(
            Guid.Parse(userId), contractId, request);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/fulfillment/reject-at-gate")]
    public async Task<IActionResult> RejectAtGate(
        Guid contractId,
        [FromBody] NileChain.Application.Dtos.Fulfillment.RejectAtGateRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.MarkRejectedAtGateAsync(
            Guid.Parse(userId), contractId, request);
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
            Guid.Parse(userId), contractId, request);
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
    [RequestSizeLimit(NileChain.Application.Validation.FileUploadValidation.MaxBytes)]
    public async Task<IActionResult> MarkPaymentMilestonePaid(
        Guid contractId,
        Guid transactionId,
        IFormFile? receipt)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _paymentMilestoneService.MarkPaidAsync(
            Guid.Parse(userId), contractId, transactionId, receipt);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/payments/mock/session")]
    public async Task<IActionResult> CreateMockPaymentSession(
        Guid contractId,
        [FromBody] NileChain.Application.Dtos.Payment.CreateMockEscrowSessionRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _mockEscrowPaymentService.CreateSessionAsync(
            Guid.Parse(userId),
            contractId,
            body.TransactionId,
            body.IdempotencyKey);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/payments/mock/{escrowId:guid}/confirm-paid")]
    public async Task<IActionResult> ConfirmMockPayment(Guid contractId, Guid escrowId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _mockEscrowPaymentService.ConfirmPaidAsync(
            Guid.Parse(userId), contractId, escrowId);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/escrow/{escrowId:guid}/confirm-release")]
    public async Task<IActionResult> ConfirmEscrowRelease(Guid contractId, Guid escrowId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _mockEscrowPaymentService.ConfirmReleaseAsync(
            Guid.Parse(userId), contractId, escrowId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/escrow")]
    public async Task<IActionResult> ListEscrow(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _mockEscrowPaymentService.ListForContractAsync(
            Guid.Parse(userId), contractId, asFarm: false);
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

    [HttpGet("disputes")]
    public async Task<IActionResult> ListMyDisputes(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _disputeService.ListMineAsync(
            Guid.Parse(userId), asFarm: false, status, page, pageSize);
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

    [HttpPost("contracts/{contractId:guid}/request-changes")]
    public async Task<IActionResult> RequestContractChanges(
        Guid contractId,
        [FromBody] RequestContractChangesRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _changeRequestService.RequestChangesAsync(
            Guid.Parse(userId),
            contractId,
            asFactory: true,
            request);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/date-amendment")]
    public async Task<IActionResult> ProposeDateAmendment(
        Guid contractId,
        [FromBody] ProposeContractDateAmendmentRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _dateAmendmentService.ProposeAsync(
            Guid.Parse(userId),
            contractId,
            asFactory: true,
            request);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/date-amendment/accept")]
    public async Task<IActionResult> AcceptDateAmendment(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _dateAmendmentService.AcceptAsync(
            Guid.Parse(userId),
            contractId,
            asFactory: true);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/date-amendment/reject")]
    public async Task<IActionResult> RejectDateAmendment(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _dateAmendmentService.RejectAsync(
            Guid.Parse(userId),
            contractId,
            asFactory: true);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/attachments")]
    public async Task<IActionResult> ListAttachments(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _attachmentService.ListAsync(Guid.Parse(userId), contractId, isFactory: true);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(
        Guid contractId,
        IFormFile file,
        [FromForm] string? kind)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var parsed = Enum.TryParse<NileChain.Domain.Enums.ContractAttachmentKind>(
            kind, ignoreCase: true, out var k)
            ? k
            : NileChain.Domain.Enums.ContractAttachmentKind.Other;

        var result = await _attachmentService.UploadAsync(
            Guid.Parse(userId), contractId, isFactory: true, file, parsed);
        return result.ToActionResult();
    }

    [HttpDelete("contracts/{contractId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid contractId, Guid attachmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _attachmentService.DeleteAsync(
            Guid.Parse(userId), contractId, attachmentId, isFactory: true);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
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
