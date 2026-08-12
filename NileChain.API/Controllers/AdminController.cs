using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.AI.Agents;
using NileChain.API.Extensions;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Dtos.Dispute;
using NileChain.Application.Interfaces;
using NileChain.Application.Validation;
using System.Security.Claims;

namespace NileChain.API.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ProactiveMonitorAgent _proactiveMonitor;
        private readonly ILogger<AdminController> _logger;
        private readonly IFulfillmentService _fulfillmentService;
        private readonly IDisputeService _disputeService;
        private readonly IMockEscrowPaymentService _mockEscrowPaymentService;

        public AdminController(
            IAdminService adminService,
            ProactiveMonitorAgent proactiveMonitor,
            ILogger<AdminController> logger,
            IFulfillmentService fulfillmentService,
            IDisputeService disputeService,
            IMockEscrowPaymentService mockEscrowPaymentService)
        {
            _adminService = adminService;
            _proactiveMonitor = proactiveMonitor;
            _logger = logger;
            _fulfillmentService = fulfillmentService;
            _disputeService = disputeService;
            _mockEscrowPaymentService = mockEscrowPaymentService;
        }

        [HttpGet("fulfillments/stuck")]
        public async Task<IActionResult> GetStuckFulfillments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _fulfillmentService.GetStuckDeliveriesAsync(page, pageSize);
            return result.ToActionResult();
        }

        [HttpPost("escrow/{escrowId:guid}/refund")]
        public async Task<IActionResult> RefundEscrow(
            Guid escrowId,
            [FromBody] NileChain.Application.Dtos.Payment.AdminEscrowRefundRequest? body)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            var result = await _mockEscrowPaymentService.AdminRefundAsync(
                Guid.Parse(userId),
                escrowId,
                body?.Reason ?? "Admin demo refund");
            return result.ToActionResult();
        }

        [HttpPost("contracts/{contractId:guid}/escrow/refund-held")]
        public async Task<IActionResult> RefundHeldEscrowForContract(
            Guid contractId,
            [FromBody] NileChain.Application.Dtos.Payment.AdminEscrowRefundRequest? body)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            var result = await _mockEscrowPaymentService.AdminRefundHeldForContractAsync(
                Guid.Parse(userId),
                contractId,
                body?.Reason ?? "Admin refund of held escrow");
            return result.ToActionResult();
        }

        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
        {
            var result = await _adminService.GetDashboardSummaryAsync(cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("contracts")]
        public async Task<IActionResult> GetContracts(
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await _adminService.GetContractsAsync(
                status, search, page, pageSize, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("disputes")]
        public async Task<IActionResult> ListDisputes(
            [FromQuery] string? status,
            [FromQuery] string? type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _disputeService.ListAdminAsync(status, type, page, pageSize);
            return result.ToActionResult();
        }

        [HttpGet("disputes/{disputeId:guid}")]
        public async Task<IActionResult> GetDispute(Guid disputeId)
        {
            var result = await _disputeService.GetAdminAsync(disputeId);
            return result.ToActionResult();
        }

        [HttpPost("disputes/{disputeId:guid}/under-review")]
        public async Task<IActionResult> MoveDisputeUnderReview(
            Guid disputeId,
            [FromBody] AdminDisputeActionRequest? request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            var result = await _disputeService.MoveToUnderReviewAsync(
                Guid.Parse(userId),
                disputeId,
                request?.AdminNote);
            return result.ToActionResult();
        }

        [HttpPost("disputes/{disputeId:guid}/resolve")]
        public async Task<IActionResult> ResolveDispute(
            Guid disputeId,
            [FromBody] AdminDisputeActionRequest request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            var result = await _disputeService.ResolveAsync(
                Guid.Parse(userId),
                disputeId,
                request.AdminNote ?? string.Empty,
                request.OutcomeFavor ?? string.Empty);
            return result.ToActionResult();
        }

        [HttpPost("disputes/{disputeId:guid}/reject")]
        public async Task<IActionResult> RejectDispute(
            Guid disputeId,
            [FromBody] AdminDisputeActionRequest request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            var result = await _disputeService.RejectAsync(
                Guid.Parse(userId),
                disputeId,
                request.AdminNote ?? string.Empty);
            return result.ToActionResult();
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? role,
            [FromQuery] bool? isVerified,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _adminService.GetUsersAsync(role, isVerified, search, page, pageSize);
            return Ok(result);
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var user = await _adminService.CreateUserAsync(request);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return SafeBadRequest(ex, "Admin.CreateUserFailed", "Could not create user.");
            }
        }

        [HttpPut("users/{id:guid}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _adminService.UpdateUserAsync(id, request);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return SafeBadRequest(ex, "Admin.UpdateUserFailed", "Could not update user.");
            }
        }

        [HttpPut("users/{id:guid}/verify")]
        public async Task<IActionResult> VerifyUser(Guid id)
        {
            var result = await _adminService.VerifyUserAsync(id);
            if (result.IsFailure)
                return result.ToActionResult();

            return Ok();
        }

        [HttpPut("users/{id:guid}/block")]
        public async Task<IActionResult> BlockUser(Guid id)
        {
            var result = await _adminService.BlockUserAsync(id);
            if (result.IsFailure)
                return result.ToActionResult();

            return Ok();
        }

        [HttpPut("users/{id:guid}/unblock")]
        public async Task<IActionResult> UnblockUser(Guid id)
        {
            var result = await _adminService.UnblockUserAsync(id);
            if (result.IsFailure)
                return result.ToActionResult();

            return Ok();
        }

        [HttpPut("users/{id:guid}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid id)
        {
            var result = await _adminService.DeactivateUserAsync(id);
            if (result.IsFailure)
                return result.ToActionResult();

            return Ok();
        }

        [HttpPut("users/{id:guid}/reactivate")]
        public async Task<IActionResult> ReactivateUser(Guid id)
        {
            var result = await _adminService.ReactivateUserAsync(id);
            if (result.IsFailure)
                return result.ToActionResult();

            return Ok();
        }

        [HttpGet("rag/documents")]
        public async Task<IActionResult> GetRagDocuments()
        {
            var result = await _adminService.GetRagDocumentsAsync();
            if (result.IsFailure)
                return result.ToActionResult();
            return Ok(result.Value);
        }

        [HttpPost("rag/upload")]
        [RequestSizeLimit(RagUploadValidation.MaxBytes)]
        public async Task<IActionResult> UploadRagDocument(
            IFormFile file,
            [FromForm] string? category,
            [FromForm] string? title)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            if (file is null || file.Length == 0)
            {
                return BadRequest(ResultHttpMapper.ToErrorBody(
                    new Error("Rag.FileEmpty", "File is required.")));
            }

            await using var probe = file.OpenReadStream();
            var validation = RagUploadValidation.Validate(file.FileName, file.Length, probe);
            if (!validation.IsValid)
            {
                return BadRequest(ResultHttpMapper.ToErrorBody(
                    new Error(validation.ErrorCode!, validation.ErrorMessage!)));
            }

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "rag");
            Directory.CreateDirectory(uploadsDir);

            var storedName = validation.SafeStoredFileName!;
            var fullPath = Path.Combine(uploadsDir, storedName);
            var displayTitle = title ?? RagUploadValidation.SanitizeDisplayName(file.FileName);

            string contentText;
            if (string.Equals(validation.Extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // PDF binary stored as-is; indexable text is title/metadata until a PDF extractor is added.
                await using (var write = System.IO.File.Create(fullPath))
                {
                    await file.CopyToAsync(write);
                }

                contentText = $"{displayTitle}\nCategory: {category}\nFile: {storedName}";
            }
            else
            {
                await using (var read = file.OpenReadStream())
                using (var reader = new StreamReader(read))
                {
                    contentText = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(contentText) || contentText.Contains('\0'))
                {
                    return BadRequest(ResultHttpMapper.ToErrorBody(
                        new Error("Rag.BinaryRejected", "Binary content is not allowed for text RAG uploads.")));
                }

                await System.IO.File.WriteAllTextAsync(fullPath, contentText);
            }

            var result = await _adminService.UploadRagDocumentAsync(
                Guid.Parse(userId),
                displayTitle,
                category,
                fullPath,
                contentText);

            if (result.IsFailure)
                return result.ToActionResult();

            return Ok(result.Value);
        }

        /// <summary>
        /// On-demand proactive monitoring pass (for demos/tests). Returns the full tool-call trail.
        /// </summary>
        [HttpPost("monitoring/run-now")]
        public async Task<IActionResult> RunMonitoringNow(CancellationToken cancellationToken)
        {
            var result = await _proactiveMonitor.RunAsync(cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    code = "Admin.MonitoringFailed",
                    message = ClientErrorSanitizer.SanitizeTrailText(
                        result.ErrorMessage) is { Length: > 0 } msg
                        ? msg
                        : "Monitoring run failed.",
                    result = new
                    {
                        result.Success,
                        ToolCallTrail = result.ToolCallTrail.Select(t => new
                        {
                            t.TimestampUtc,
                            t.FunctionName,
                            ArgumentsSummary = ClientErrorSanitizer.SanitizeTrailText(t.ArgumentsSummary),
                            ResultSummary = ClientErrorSanitizer.SanitizeTrailText(t.ResultSummary),
                            t.Blocked,
                            BlockReason = t.BlockReason is null
                                ? null
                                : ClientErrorSanitizer.SanitizeTrailText(t.BlockReason)
                        })
                    }
                });
            }

            return Ok(result);
        }

        private IActionResult SafeBadRequest(Exception ex, string code, string safeMessage)
        {
            _logger.LogWarning(ex, "Admin operation failed with code {Code}", code);
            return BadRequest(ResultHttpMapper.ToErrorBody(new Error(code, safeMessage)));
        }
    }
}
