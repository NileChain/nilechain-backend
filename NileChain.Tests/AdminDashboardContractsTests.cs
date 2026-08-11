using NileChain.Application.Services;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NileChain.Tests;

public class AdminDashboardContractsTests
{
    private static AdminService CreateSut(IAdminAnalyticsRepository analytics)
    {
        var users = new UserManager<ApplicationUser>(
            new FakeUserStore(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new LoggerFactory().CreateLogger<UserManager<ApplicationUser>>());

        var roles = new RoleManager<ApplicationRole>(
            new FakeRoleStore(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new LoggerFactory().CreateLogger<RoleManager<ApplicationRole>>());

        // Dashboard/contracts paths only use analytics — other deps are unused here.
        return new AdminService(
            users,
            roles,
            null!,
            null!,
            null!,
            null!,
            analytics,
            null!);
    }

    [Fact]
    public async Task GetDashboardSummary_MapsCountsAndMonthlyBars()
    {
        var analytics = new FakeAnalytics
        {
            Unverified = 3,
            AllUsers = 20,
            OpenDisputes = 2,
            Stuck = 1,
            StatusCount = 4,
            Farms = 10,
            Factories = 5,
            Admins = 2,
            Monthly = [(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 8)],
            Crops = [("Wheat", 500m, 8000m, 75m)],
            Activity = [("contract", "Contract ABCD1234 — Signed", DateTime.UtcNow, "description")]
        };

        var result = await CreateSut(analytics).GetDashboardSummaryAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.PendingVerifications);
        Assert.Equal(2, result.Value.OpenDisputes);
        Assert.Equal(1, result.Value.StuckFulfillments);
        Assert.Equal(12, result.Value.PendingSignatureContracts);
        Assert.Equal(4, result.Value.SignedContracts);
        Assert.Equal(20, result.Value.TotalUsers);
        Assert.Single(result.Value.TopCrops);
        Assert.Equal("low", result.Value.TopCrops[0].RiskBand);
        Assert.Contains(result.Value.MonthlyContracts, m => m.Count == 8 && m.HeightPercent == 100);
    }

    [Fact]
    public async Task GetContracts_MapsValueAndStatusBands()
    {
        var contractId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var analytics = new FakeAnalytics
        {
            ContractTotal = 1,
            Contracts =
            [
                new AdminContractRow(
                    contractId,
                    "Nile Farm",
                    "Cairo Mills",
                    "Wheat",
                    100m,
                    50m,
                    82m,
                    ContractStatus.Signed,
                    DateTime.UtcNow)
            ]
        };

        var result = await CreateSut(analytics).GetContractsAsync(null, null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("AAAAAAAA", item.ShortId);
        Assert.Equal(5000m, item.ValueEgp);
        Assert.Equal("signed", item.Status);
        Assert.Equal("low", item.RiskBand);
    }

    private sealed class FakeAnalytics : IAdminAnalyticsRepository
    {
        public int Unverified { get; init; }
        public int AllUsers { get; init; }
        public int OpenDisputes { get; init; }
        public int Stuck { get; init; }
        public int StatusCount { get; init; }
        public int Farms { get; init; }
        public int Factories { get; init; }
        public int Admins { get; init; }
        public List<(int, int, int)> Monthly { get; init; } = [];
        public List<(string, decimal, decimal?, decimal?)> Crops { get; init; } = [];
        public List<(string, string, DateTime, string)> Activity { get; init; } = [];
        public int ContractTotal { get; init; }
        public List<AdminContractRow> Contracts { get; init; } = [];

        public Task<int> CountUnverifiedUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Unverified);
        public Task<int> CountAllUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AllUsers);
        public Task<int> CountUsersInRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) =>
            Task.FromResult(Admins);
        public Task<int> CountFarmsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Farms);
        public Task<int> CountFactoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Factories);
        public Task<int> CountContractsByStatusAsync(ContractStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult(StatusCount);
        public Task<int> CountOpenDisputesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OpenDisputes);
        public Task<int> CountStuckFulfillmentsAsync(DateTime asOfUtcNoon, CancellationToken cancellationToken = default) =>
            Task.FromResult(Stuck);
        public Task<IReadOnlyList<(int Year, int Month, int Count)>> GetMonthlyContractCountsAsync(
            DateTime fromUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<(int, int, int)>)Monthly);
        public Task<IReadOnlyList<(string CropName, decimal DemandTons, decimal? AvgPrice, decimal? AvgRisk)>> GetTopCropDemandAsync(
            int take, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<(string, decimal, decimal?, decimal?)>)Crops);
        public Task<IReadOnlyList<(string Kind, string Message, DateTime OccurredAt, string Icon)>> GetRecentActivityAsync(
            int take, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<(string, string, DateTime, string)>)Activity);
        public Task<(int Total, IReadOnlyList<AdminContractRow> Items)> GetContractsAsync(
            string? status, string? search, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult((ContractTotal, (IReadOnlyList<AdminContractRow>)Contracts));
    }

    private sealed class FakeUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
    }

    private sealed class FakeRoleStore : IRoleStore<ApplicationRole>
    {
        public void Dispose() { }
        public Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationRole?>(null);
        public Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationRole?>(null);
        public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
            Task.FromResult(role.NormalizedName);
        public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken) =>
            Task.FromResult(role.Id.ToString());
        public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
            Task.FromResult(role.Name);
        public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
    }
}
