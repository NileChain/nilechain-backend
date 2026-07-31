using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
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
    }
}
