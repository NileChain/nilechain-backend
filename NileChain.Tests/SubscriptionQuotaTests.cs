using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Common;
using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class SubscriptionQuotaTests
{
    [Fact]
    public void Entitlements_FreeFactory_CapsAndFlags()
    {
        var e = SubscriptionEntitlementsResolver.For(SubscriptionPlanCodes.FactoryFree);
        Assert.Equal(2, e.FactoryRfqs);
        Assert.Equal(2, e.AgentRuns);
        Assert.False(e.Copilot);
        Assert.False(e.ShowMore);
        Assert.False(e.ExpandGeo);
    }

    [Fact]
    public void Entitlements_ProFactory_UnlocksCopilotAndUnlimitedRuns()
    {
        var e = SubscriptionEntitlementsResolver.For(SubscriptionPlanCodes.FactoryPro);
        Assert.Equal(20, e.FactoryRfqs);
        Assert.Null(e.AgentRuns);
        Assert.True(e.Copilot);
        Assert.True(e.ShowMore);
        Assert.True(e.ExpandGeo);
    }

    [Fact]
    public void Entitlements_FreeFarm_ThreeAccepts()
    {
        var e = SubscriptionEntitlementsResolver.For(SubscriptionPlanCodes.FarmFree);
        Assert.Equal(3, e.FarmAccepts);
        Assert.False(e.Copilot);
    }

    [Fact]
    public void ResultHttpMapper_QuotaAndPlan_AreForbidden()
    {
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, ResultHttpMapper.MapStatus(SubscriptionErrors.QuotaExceeded));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, ResultHttpMapper.MapStatus(SubscriptionErrors.PlanRequired));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, ResultHttpMapper.MapStatus(AuthErrors.KybPending));
    }

    [Fact]
    public async Task Farm_FourthAccept_Fails_RejectStaysFree()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFarmAsync(harness.Db);
        var sut = CreateSubscriptionService(harness, seeded);

        for (var i = 0; i < 3; i++)
        {
            var ok = await sut.EnsureCanConsumeAsync(seeded.FarmUserId, SubscriptionMetric.FarmAccepts);
            Assert.True(ok.IsSuccess, ok.Error?.Description);
            await sut.ConsumeAsync(seeded.FarmUserId, SubscriptionMetric.FarmAccepts);
        }

        var fourth = await sut.EnsureCanConsumeAsync(seeded.FarmUserId, SubscriptionMetric.FarmAccepts);
        Assert.True(fourth.IsFailure);
        Assert.Equal(SubscriptionErrors.QuotaExceeded.Code, fourth.Error!.Code);
    }

    [Fact]
    public async Task Factory_ThirdRfqAndRun_Fail_CopilotRequiresPro()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFactoryAsync(harness.Db);
        var sut = CreateSubscriptionService(harness, seeded);

        for (var i = 0; i < 2; i++)
        {
            Assert.True((await sut.EnsureCanConsumeAsync(seeded.FactoryUserId, SubscriptionMetric.FactoryRfqs)).IsSuccess);
            await sut.ConsumeAsync(seeded.FactoryUserId, SubscriptionMetric.FactoryRfqs);
            Assert.True((await sut.EnsureCanConsumeAsync(seeded.FactoryUserId, SubscriptionMetric.AgentRuns)).IsSuccess);
            await sut.ConsumeAsync(seeded.FactoryUserId, SubscriptionMetric.AgentRuns);
        }

        var thirdRfq = await sut.EnsureCanConsumeAsync(seeded.FactoryUserId, SubscriptionMetric.FactoryRfqs);
        Assert.Equal(SubscriptionErrors.QuotaExceeded.Code, thirdRfq.Error!.Code);

        var thirdRun = await sut.EnsureCanConsumeAsync(seeded.FactoryUserId, SubscriptionMetric.AgentRuns);
        Assert.Equal(SubscriptionErrors.QuotaExceeded.Code, thirdRun.Error!.Code);

        var copilot = await sut.EnsureFeatureAsync(seeded.FactoryUserId, SubscriptionFeatureFlag.Copilot);
        Assert.Equal(SubscriptionErrors.PlanRequired.Code, copilot.Error!.Code);
    }

    [Fact]
    public async Task WalletSubscribe_DebitsAndFlipsEntitlements()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFactoryAsync(harness.Db, walletAvailable: 2000m);
        var sut = CreateSubscriptionService(harness, seeded);

        var before = await sut.GetMineAsync(seeded.FactoryUserId, asFarm: false);
        Assert.Equal(SubscriptionPlanCodes.FactoryFree, before.Value!.PlanCode);

        var paid = await sut.SubscribeAsync(seeded.FactoryUserId, asFarm: false);
        Assert.True(paid.IsSuccess, paid.Error?.Description);
        Assert.Equal(SubscriptionPlanCodes.FactoryPro, paid.Value!.PlanCode);
        Assert.True(paid.Value.Copilot);
        Assert.True(paid.Value.PeriodEnd > DateTime.UtcNow.AddDays(27));

        var wallet = await harness.Db.Wallets.AsNoTracking()
            .SingleAsync(w => w.OwnerId == seeded.FactoryId);
        Assert.Equal(500m, wallet.AvailableBalanceEgp);
    }

    [Fact]
    public async Task AdminGrant_BypassesWallet()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFactoryAsync(harness.Db, walletAvailable: 10m);
        var sut = CreateSubscriptionService(harness, seeded);

        var granted = await sut.AdminGrantAsync(
            seeded.FactoryUserId,
            SubscriptionPlanCodes.FactoryPro,
            DateTime.UtcNow.AddDays(10));
        Assert.True(granted.IsSuccess, granted.Error?.Description);
        Assert.True(granted.Value!.IsPro);
        Assert.Equal("AdminGrant", granted.Value.Source);

        var wallet = await harness.Db.Wallets.AsNoTracking()
            .SingleAsync(w => w.OwnerId == seeded.FactoryId);
        Assert.Equal(10m, wallet.AvailableBalanceEgp);

        var copilot = await sut.EnsureFeatureAsync(seeded.FactoryUserId, SubscriptionFeatureFlag.Copilot);
        Assert.True(copilot.IsSuccess);
    }

    [Fact]
    public async Task AdminRevoke_ReturnsToFree()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFarmAsync(harness.Db);
        var sut = CreateSubscriptionService(harness, seeded);

        Assert.True((await sut.AdminGrantAsync(seeded.FarmUserId, SubscriptionPlanCodes.FarmPro, null)).IsSuccess);
        var revoked = await sut.AdminGrantAsync(seeded.FarmUserId, SubscriptionPlanCodes.FarmFree, null);
        Assert.True(revoked.IsSuccess);
        Assert.Equal(SubscriptionPlanCodes.FarmFree, revoked.Value!.PlanCode);
        Assert.Equal(3, revoked.Value.FarmAccepts.Cap);
    }

    private static SubscriptionService CreateSubscriptionService(SqliteHarness harness, SeededUsers seeded)
    {
        var users = CreateUserManager(seeded);
        return new SubscriptionService(
            new SubscriptionRepository(harness.Db),
            CreateWalletService(harness.Db),
            new UnitOfWork(harness.Db),
            users,
            Options.Create(new SubscriptionOptions()));
    }

    private static WalletService CreateWalletService(NileChainDbContext db) =>
        new(
            new WalletRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new UnitOfWork(db),
            new NoopPaymob(),
            Options.Create(new MockPaymentOptions()),
            Options.Create(new PaymobOptions()),
            NullLogger<WalletService>.Instance,
            Options.Create(new SubscriptionOptions()));

    private static UserManager<ApplicationUser> CreateUserManager(SeededUsers seeded)
    {
        var store = new MemoryUserStore();
        store.Add(seeded.FarmUser, AppRoles.Farm);
        store.Add(seeded.FactoryUser, AppRoles.Factory);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new LoggerFactory().CreateLogger<UserManager<ApplicationUser>>());
    }

    private static async Task<SeededUsers> SeedFactoryAsync(NileChainDbContext db, decimal walletAvailable = 2000m)
    {
        var users = await SeedBothAsync(db, walletAvailable);
        return users;
    }

    private static Task<SeededUsers> SeedFarmAsync(NileChainDbContext db) =>
        SeedBothAsync(db, walletAvailable: 2000m);

    private static async Task<SeededUsers> SeedBothAsync(NileChainDbContext db, decimal walletAvailable)
    {
        var farmUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "farm-sub@test.local",
            NormalizedUserName = "FARM-SUB@TEST.LOCAL",
            Email = "farm-sub@test.local",
            NormalizedEmail = "FARM-SUB@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            IsVerified = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var factoryUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "factory-sub@test.local",
            NormalizedUserName = "FACTORY-SUB@TEST.LOCAL",
            Email = "factory-sub@test.local",
            NormalizedEmail = "FACTORY-SUB@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            IsVerified = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        db.Users.Add(farmUser);
        db.Users.Add(factoryUser);

        var farmId = Guid.NewGuid();
        var factoryId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUser.Id,
            Name = "Quota Farm",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUser.Id,
            Name = "Quota Factory",
            CreatedAt = DateTime.UtcNow
        });
        db.Wallets.Add(new Wallet
        {
            WalletId = Guid.NewGuid(),
            OwnerType = WalletOwnerType.Factory,
            OwnerId = factoryId,
            AvailableBalanceEgp = walletAvailable,
            HeldBalanceEgp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1]
        });
        db.Wallets.Add(new Wallet
        {
            WalletId = Guid.NewGuid(),
            OwnerType = WalletOwnerType.Farm,
            OwnerId = farmId,
            AvailableBalanceEgp = walletAvailable,
            HeldBalanceEgp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1]
        });
        await db.SaveChangesAsync();
        return new SeededUsers(farmUser, factoryUser, farmId, factoryId);
    }

    private sealed record SeededUsers(
        ApplicationUser FarmUser,
        ApplicationUser FactoryUser,
        Guid FarmId,
        Guid FactoryId)
    {
        public Guid FarmUserId => FarmUser.Id;
        public Guid FactoryUserId => FactoryUser.Id;
    }

    private sealed class MemoryUserStore : IUserStore<ApplicationUser>, IUserRoleStore<ApplicationUser>
    {
        private readonly Dictionary<Guid, ApplicationUser> _users = new();
        private readonly Dictionary<Guid, List<string>> _roles = new();

        public void Add(ApplicationUser user, string role)
        {
            _users[user.Id] = user;
            _roles[user.Id] = [role];
        }

        public void Dispose() { }
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_users.TryGetValue(Guid.Parse(userId), out var u) ? u : null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult(_users.Values.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName));
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
        public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            _roles[user.Id].Add(roleName);
            return Task.CompletedTask;
        }
        public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            _roles[user.Id].Remove(roleName);
            return Task.CompletedTask;
        }
        public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult((IList<string>)(_roles.GetValueOrDefault(user.Id) ?? []));
        public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken) =>
            Task.FromResult((_roles.GetValueOrDefault(user.Id) ?? []).Contains(roleName, StringComparer.OrdinalIgnoreCase));
        public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken) =>
            Task.FromResult((IList<ApplicationUser>)_users.Values
                .Where(u => (_roles.GetValueOrDefault(u.Id) ?? []).Contains(roleName, StringComparer.OrdinalIgnoreCase))
                .ToList());
    }

    private sealed class NoopPaymob : IPaymobClient
    {
        public bool IsConfigured => false;
        public Task<PaymobIntentionResult> CreateIntentionAsync(
            PaymobIntentionRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new PaymobIntentionResult { Success = false, Error = "noop" });
        public bool VerifyHmac(IReadOnlyDictionary<string, string?> obj, string receivedHmac) => false;
        public string BuildCheckoutUrl(string clientSecret) => string.Empty;
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        public NileChainDbContext Db { get; }

        private SqliteHarness(SqliteConnection keepAlive, NileChainDbContext db)
        {
            _keepAlive = keepAlive;
            Db = db;
        }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var cs = $"Data Source=file:sub-quota-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(cs);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(cs)
                .Options;
            var db = new SubDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class SubDbContext : NileChainDbContext
    {
        public SubDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<Wallet>()
                .Property(w => w.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<FarmMatch>()
                .Property(m => m.EligibilitySnapshotJson)
                .HasColumnType("TEXT");
        }
    }
}
