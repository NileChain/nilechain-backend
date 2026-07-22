using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories
{
    public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
    {
        private readonly NileChainDbContext _context;

        public RefreshTokenRepository(NileChainDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        {
            return await _context.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        }

        public Task RevokeAsync(RefreshToken token)
        {
            token.RevokedAt = DateTime.UtcNow;

            _context.RefreshTokens.Update(token);

            return Task.CompletedTask;
        }

        public async Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId)
        {
            return await _context.RefreshTokens
                .Where(x => x.UserId == userId &&
                            x.RevokedAt == null &&
                            x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();
        }
    }
}
