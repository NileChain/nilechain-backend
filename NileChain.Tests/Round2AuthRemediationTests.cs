using NileChain.Application.Auth;
using NileChain.Domain.Entities;
using NileChain.Domain.Identity;

namespace NileChain.Tests;

public class AdminRoleAllowlistTests
{
    [Theory]
    [InlineData("farm", "Farm")]
    [InlineData("Farm", "Farm")]
    [InlineData("FACTORY", "Factory")]
    [InlineData(" admin ", "Admin")]
    public void NormalizeOrThrow_AllowedRoles(string input, string expected)
    {
        Assert.Equal(expected, AdminRoleAllowlist.NormalizeOrThrow(input));
    }

    [Fact]
    public void NormalizeOrThrow_RejectsSuperAdmin()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AdminRoleAllowlist.NormalizeOrThrow("SuperAdmin"));
        Assert.Contains("SuperAdmin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("garbage")]
    [InlineData("root")]
    public void NormalizeOrThrow_RejectsUnrecognizedRoles(string role)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AdminRoleAllowlist.NormalizeOrThrow(role));
        Assert.Contains("not allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeOrThrow_RejectsEmpty(string? role)
    {
        Assert.Throws<InvalidOperationException>(
            () => AdminRoleAllowlist.NormalizeOrThrow(role));
    }
}

public class AuthTokenValidationTests
{
    [Fact]
    public void IsAccessAllowed_ActiveUnlocked_True()
    {
        var user = new ApplicationUser { IsActive = true };
        Assert.True(AuthTokenValidation.IsAccessAllowed(user, isLockedOut: false));
    }

    [Fact]
    public void IsAccessAllowed_Inactive_False()
    {
        var user = new ApplicationUser { IsActive = false };
        Assert.False(AuthTokenValidation.IsAccessAllowed(user, isLockedOut: false));
    }

    [Fact]
    public void IsAccessAllowed_LockedOut_False()
    {
        var user = new ApplicationUser { IsActive = true };
        Assert.False(AuthTokenValidation.IsAccessAllowed(user, isLockedOut: true));
    }

    [Fact]
    public void IsAccessAllowed_InactiveAndLocked_False()
    {
        var user = new ApplicationUser { IsActive = false };
        Assert.False(AuthTokenValidation.IsAccessAllowed(user, isLockedOut: true));
    }
}

public class RefreshTokenRevocationTests
{
    [Fact]
    public void MarkAllRevoked_SetsRevokedAtOnActiveTokens()
    {
        var now = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
        var tokens = new List<RefreshToken>
        {
            new() { Id = Guid.NewGuid(), TokenHash = "a", ExpiresAt = now.AddDays(1) },
            new() { Id = Guid.NewGuid(), TokenHash = "b", ExpiresAt = now.AddDays(1), RevokedAt = now.AddHours(-1) },
            new() { Id = Guid.NewGuid(), TokenHash = "c", ExpiresAt = now.AddDays(1) },
        };

        var count = RefreshTokenRevocation.MarkAllRevoked(tokens, now);

        Assert.Equal(2, count);
        Assert.Equal(now, tokens[0].RevokedAt);
        Assert.Equal(now.AddHours(-1), tokens[1].RevokedAt);
        Assert.Equal(now, tokens[2].RevokedAt);
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }
}
