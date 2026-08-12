using NileChain.API.Extensions;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Interfaces;
using NileChain.Application.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NileChain.Domain.Constants;

namespace NileChain.API.Controllers;

[Route("api/farm")]
[ApiController]
[Authorize(Roles = AppRoles.Farm)]
public class FarmController : ControllerBase
{
    private readonly IFarmService _farmService;
    private readonly IFulfillmentService _fulfillmentService;
    private readonly IPaymentMilestoneService _paymentMilestoneService;
    private readonly IMockEscrowPaymentService _mockEscrowPaymentService;
    private readonly IDisputeService _disputeService;
    private readonly IContractAttachmentService _attachmentService;

    public FarmController(
        IFarmService farmService,
        IFulfillmentService fulfillmentService,
        IPaymentMilestoneService paymentMilestoneService,
        IMockEscrowPaymentService mockEscrowPaymentService,
        IDisputeService disputeService,
        IContractAttachmentService attachmentService)
    {
        _farmService = farmService;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
        _mockEscrowPaymentService = mockEscrowPaymentService;
        _disputeService = disputeService;
        _attachmentService = attachmentService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetProfileAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetDashboardAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateFarmProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.UpdateProfileAsync(Guid.Parse(userId), request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetDocumentsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPost("documents")]
    public async Task<IActionResult> AddDocument(IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.AddDocumentAsync(Guid.Parse(userId), file);
        return result.ToActionResult();
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.DeleteDocumentAsync(Guid.Parse(userId), documentId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("crops")]
    public async Task<IActionResult> AddCrop(AddCropRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.AddCropAsync(Guid.Parse(userId), request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPut("crops/{cropTypeId:guid}")]
    public async Task<IActionResult> UpdateCrop(Guid cropTypeId, UpdateFarmCropRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.UpdateCropAsync(Guid.Parse(userId), cropTypeId, request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("images")]
    public async Task<IActionResult> AddImage(IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.AddImageAsync(Guid.Parse(userId), file);
        return result.ToActionResult();
    }

    [HttpDelete("images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid imageId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.DeleteImageAsync(Guid.Parse(userId), imageId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("matches/{matchId:guid}/counter-offer")]
    public async Task<IActionResult> CounterOffer(Guid matchId, [FromBody] CounterOfferRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.CounterOfferAsync(Guid.Parse(userId), matchId, request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpDelete("crops/{cropTypeId:guid}")]
    public async Task<IActionResult> DeleteCrop(Guid cropTypeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.DeleteCropAsync(Guid.Parse(userId), cropTypeId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("certifications")]
    public async Task<IActionResult> GetCertifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetCertificationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPost("certifications")]
    public async Task<IActionResult> AddCertification(AddFarmCertificationRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.AddCertificationAsync(Guid.Parse(userId), request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpDelete("certifications/{certificationId:guid}")]
    public async Task<IActionResult> DeleteCertification(Guid certificationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.DeleteCertificationAsync(Guid.Parse(userId), certificationId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches(
        [FromQuery] string? status,
        [FromQuery] Guid? cropTypeId,
        [FromQuery] string? sort,
        [FromQuery] string? search,
        [FromQuery] int? days,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetMatchesAsync(
            Guid.Parse(userId),
            status,
            cropTypeId,
            sort,
            search,
            days,
            page,
            pageSize);
        return result.ToActionResult();
    }

    [HttpPut("matches/{matchId:guid}/respond")]
    public async Task<IActionResult> RespondToMatch(Guid matchId, [FromBody] RespondToMatchRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.RespondToMatchAsync(Guid.Parse(userId), matchId, request.Action);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("matches/{matchId:guid}/contract")]
    public async Task<IActionResult> GetOrCreateContractForMatch(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetOrCreateContractForMatchAsync(Guid.Parse(userId), matchId);
        return result.ToActionResult();
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetContractsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}")]
    public async Task<IActionResult> GetContract(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetContractAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/attachments")]
    public async Task<IActionResult> ListAttachments(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _attachmentService.ListAsync(Guid.Parse(userId), contractId, isFactory: false);
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
            Guid.Parse(userId), contractId, isFactory: false, file, parsed);
        return result.ToActionResult();
    }

    [HttpDelete("contracts/{contractId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid contractId, Guid attachmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _attachmentService.DeleteAsync(
            Guid.Parse(userId), contractId, attachmentId, isFactory: false);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/fulfillment")]
    public async Task<IActionResult> GetFulfillment(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.GetByContractAsync(
            Guid.Parse(userId), contractId, asFarm: true);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/fulfillment/ship")]
    public async Task<IActionResult> MarkShipped(
        Guid contractId,
        [FromBody] NileChain.Application.Dtos.Fulfillment.ShipFulfillmentRequest? request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _fulfillmentService.MarkShippedAsync(
            Guid.Parse(userId), contractId, request);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/payment-milestones")]
    public async Task<IActionResult> GetPaymentMilestones(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _paymentMilestoneService.GetByContractAsync(
            Guid.Parse(userId), contractId, asFarm: true);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/payment-milestones/{transactionId:guid}/confirm-received")]
    public async Task<IActionResult> ConfirmPaymentMilestoneReceived(Guid contractId, Guid transactionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _paymentMilestoneService.ConfirmReceivedAsync(
            Guid.Parse(userId), contractId, transactionId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/escrow")]
    public async Task<IActionResult> ListEscrow(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _mockEscrowPaymentService.ListForContractAsync(
            Guid.Parse(userId), contractId, asFarm: true);
        return result.ToActionResult();
    }

    [HttpPut("contracts/{contractId:guid}/approve")]
    public async Task<IActionResult> ApproveContract(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.ApproveContractAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpPut("contracts/{contractId:guid}/reject")]
    public async Task<IActionResult> RejectContract(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.RejectContractAsync(Guid.Parse(userId), contractId);
        return result.ToActionResult();
    }

    [HttpGet("contracts/{contractId:guid}/pdf")]
    public async Task<IActionResult> DownloadContractPdf(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetContractPdfAsync(Guid.Parse(userId), contractId);
        if (!result.IsSuccess)
            return result.ToActionResult();

        var (bytes, fileName) = result.Value!;
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("contracts/{contractId:guid}/disputes")]
    public async Task<IActionResult> ListDisputes(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _disputeService.ListForContractAsync(
            Guid.Parse(userId), contractId, asFarm: true);
        return result.ToActionResult();
    }

    [HttpPost("contracts/{contractId:guid}/disputes")]
    [RequestSizeLimit(FileUploadValidation.MaxBytes * 5)]
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
            asFarm: true,
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
            Guid.Parse(userId), asFarm: true, status, page, pageSize);
        return result.ToActionResult();
    }

    [HttpGet("disputes/{disputeId:guid}")]
    public async Task<IActionResult> GetDispute(Guid disputeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _disputeService.GetAsync(Guid.Parse(userId), disputeId, asFarm: true);
        return result.ToActionResult();
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetConversationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("factories/{factoryId:guid}/public-profile")]
    public async Task<IActionResult> GetMatchedFactoryPublicProfile(Guid factoryId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetMatchedFactoryPublicProfileAsync(
            Guid.Parse(userId),
            factoryId);
        return result.ToActionResult();
    }

    [HttpGet("conversations/{matchId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetMessagesAsync(Guid.Parse(userId), matchId);
        return result.ToActionResult();
    }

    [HttpPost("conversations/{matchId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid matchId, [FromBody] NileChain.Application.Dtos.Farm.SendMessageRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.SendMessageAsync(Guid.Parse(userId), matchId, request.Content);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetNotificationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(Guid notificationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.MarkNotificationAsReadAsync(Guid.Parse(userId), notificationId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
