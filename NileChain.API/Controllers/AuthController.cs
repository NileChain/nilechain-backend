using NileChain.API.Extensions;
using NileChain.API.Filters;
using NileChain.Application.Dtos.Auth.Requests;
using NileChain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NileChain.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [AllowUnverified]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            return result.ToActionResult();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            return result.ToActionResult();
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(RefreshTokenRequest request)
        {
            var result =
                await _authService.RefreshTokenAsync(request);

            return result.ToActionResult();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId is null)
            {
                return Unauthorized();
            }

            var result =
                await _authService.GetCurrentUserAsync(
                    Guid.Parse(userId));

            return result.ToActionResult();
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(Guid userId, string token)
        {
            var result =
                await _authService.ConfirmEmailAsync(
                    userId,
                    token);

            return result.ToActionResult();
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            var result =
                await _authService.ForgotPasswordAsync(request);

            return result.ToActionResult();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var result =
                await _authService.ResetPasswordAsync(request);

            return result.ToActionResult();
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(RefreshTokenRequest request)
        {
            var result =
                await _authService.LogoutAsync(request.RefreshToken);

            return result.ToActionResult();
        }

        [Authorize]
        [HttpPut("phone")]
        public async Task<IActionResult> UpdatePhone(UpdatePhoneRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Unauthorized();

            var result = await _authService.UpdatePhoneAsync(Guid.Parse(userId), request.PhoneNumber);
            return result.IsSuccess ? NoContent() : result.ToActionResult();
        }
    }
}
