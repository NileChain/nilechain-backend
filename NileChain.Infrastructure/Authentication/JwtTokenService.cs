using NileChain.Application.Interfaces;
using NileChain.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NileChain.Infrastructure.Authentication
{
    public class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _jwtOptions;

        public JwtTokenService(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public (string Token, DateTime ExpiresAt) GenerateAccessToken(
    ApplicationUser user,
    IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email!),

                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // Needed on full-page loads (Paymob return) before /me hydrates.
                new("is_verified", user.IsVerified ? "true" : "false")
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.Secret));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var expiresAt =
                    DateTime.UtcNow.AddMinutes(
                        _jwtOptions.AccessTokenExpirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (
                    new JwtSecurityTokenHandler().WriteToken(token),
                    expiresAt
                );
        }

        public (string Token, DateTime ExpiresAt) GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);

            var token = Convert.ToBase64String(bytes);

            var expiresAt = DateTime.UtcNow.AddDays(
                _jwtOptions.RefreshTokenExpirationDays);

            return (token, expiresAt);
        }

        public string HashRefreshToken(string refreshToken)
        {
            var hash = SHA256.HashData(
                Encoding.UTF8.GetBytes(refreshToken));

            return Convert.ToHexString(hash);
        }
    }
}
