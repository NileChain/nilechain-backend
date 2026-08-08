using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.AI.Agents;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Interfaces;

namespace NileChain.API.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ProactiveMonitorAgent _proactiveMonitor;

        public AdminController(
            IAdminService adminService,
            ProactiveMonitorAgent proactiveMonitor)
        {
            _adminService = adminService;
            _proactiveMonitor = proactiveMonitor;
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
                return BadRequest(new { error = ex.Message });
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
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("users/{id:guid}/verify")]
        public async Task<IActionResult> VerifyUser(Guid id)
        {
            var result = await _adminService.VerifyUserAsync(id);
            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });

            return Ok();
        }

        [HttpPut("users/{id:guid}/block")]
        public async Task<IActionResult> BlockUser(Guid id)
        {
            var result = await _adminService.BlockUserAsync(id);
            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });

            return Ok();
        }

        [HttpPut("users/{id:guid}/unblock")]
        public async Task<IActionResult> UnblockUser(Guid id)
        {
            var result = await _adminService.UnblockUserAsync(id);
            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });

            return Ok();
        }

        [HttpPut("users/{id:guid}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid id)
        {
            var result = await _adminService.DeactivateUserAsync(id);
            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });

            return Ok();
        }

        [HttpPut("users/{id:guid}/reactivate")]
        public async Task<IActionResult> ReactivateUser(Guid id)
        {
            var result = await _adminService.ReactivateUserAsync(id);
            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });

            return Ok();
        }

        [HttpGet("rag/documents")]
        public async Task<IActionResult> GetRagDocuments()
        {
            var result = await _adminService.GetRagDocumentsAsync();
            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });
            return Ok(result.Value);
        }

        [HttpPost("rag/upload")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> UploadRagDocument(
            IFormFile file,
            [FromForm] string? category,
            [FromForm] string? title)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
                return Unauthorized();

            if (file is null || file.Length == 0)
                return BadRequest(new { error = "File is required." });

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "rag");
            Directory.CreateDirectory(uploadsDir);

            var safeName = Path.GetFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}_{safeName}";
            var fullPath = Path.Combine(uploadsDir, storedName);

            string contentText;
            await using (var read = file.OpenReadStream())
            using (var reader = new StreamReader(read))
            {
                contentText = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(contentText) || contentText.Contains('\0'))
            {
                contentText = $"{title ?? safeName}\nCategory: {category}\nFile: {safeName}";
            }

            await System.IO.File.WriteAllTextAsync(fullPath, contentText);

            var result = await _adminService.UploadRagDocumentAsync(
                Guid.Parse(userId),
                title ?? Path.GetFileNameWithoutExtension(safeName),
                category,
                fullPath,
                contentText);

            if (result.IsFailure)
                return BadRequest(new { error = result.Error?.Description });

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
                return BadRequest(new { error = result.ErrorMessage ?? "Monitoring run failed.", result });

            return Ok(result);
        }
    }
}
