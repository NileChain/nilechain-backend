using NileChain.Application.Common;
using NileChain.Application.Dtos.Auth.Requests;
using NileChain.Application.Dtos.Auth.Responses;

namespace NileChain.Application.Interfaces
{
    public interface IAuthService
    {
        Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request);

        Task<Result<AuthResponse>> LoginAsync(LoginRequest request);

        Task<Result<AuthResponse>> RefreshTokenAsync(
            RefreshTokenRequest request);

        Task<Result<CurrentUserResponse>> GetCurrentUserAsync(Guid userId);

        Task<Result> LogoutAsync(string refreshToken);

        Task<Result> ConfirmEmailAsync(Guid userId, string token);

        Task<Result> ResetPasswordAsync(ResetPasswordRequest request);
        Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<Result> UpdatePhoneAsync(Guid userId, string phoneNumber);
    }
}
