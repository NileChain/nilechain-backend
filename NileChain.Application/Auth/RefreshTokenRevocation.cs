using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Auth;

/// <summary>
/// Shared revoke-all-active-refresh-tokens for a user (password reset, block, deactivate, rotation).
/// </summary>
public static class RefreshTokenRevocation
{
    public static async Task RevokeAllActiveAsync(
        IRefreshTokenRepository repository,
        Guid userId)
    {
        var activeTokens = await repository.GetActiveTokensByUserIdAsync(userId);
        foreach (var token in activeTokens)
            await repository.RevokeAsync(token);
    }

    /// <summary>
    /// In-memory revoke side-effect (same as repository RevokeAsync), for unit tests / helpers.
    /// </summary>
    public static int MarkAllRevoked(IEnumerable<RefreshToken> tokens, DateTime utcNow)
    {
        var count = 0;
        foreach (var token in tokens)
        {
            if (token.RevokedAt is not null)
                continue;

            token.RevokedAt = utcNow;
            count++;
        }

        return count;
    }
}
