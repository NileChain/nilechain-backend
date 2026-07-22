using NileChain.Domain.Identity;

namespace NileChain.Application.Interfaces
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) GenerateAccessToken(
            ApplicationUser user,
            IList<string> roles);

        (string Token, DateTime ExpiresAt) GenerateRefreshToken();

        string HashRefreshToken(string refreshToken);
    }
}
