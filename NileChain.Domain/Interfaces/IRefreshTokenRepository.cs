using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces
{
    public interface IRefreshTokenRepository : IRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

        Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId);

        Task RevokeAsync(RefreshToken token);
    }
}
