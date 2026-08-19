using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class WalletSubscriptionTests
{
    [Fact]
    public async Task PayMonth_DebitsFactory_CreditsPlatform_ExtendsPaidThrough()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        // Two months of subscription, so the second charge is affordable and can extend.
        var seeded = await SeedFactoryWalletAsync(harness.Db, available: 3000m);
        var wallets = CreateWalletService(harness.Db);

        var paid = await wallets.PaySubscriptionMonthAsync(seeded.FactoryUserId, asFarm: false);
        Assert.True(paid.IsSuccess);
        Assert.True(paid.Value!.SubscriptionApplies);
        Assert.True(paid.Value.SubscriptionActive);
        Assert.Equal(30m, paid.Value.SubscriptionMonthlyUsd);
        Assert.Equal(1500m, paid.Value.SubscriptionMonthlyEgp);
        Assert.Equal(1500m, paid.Value.AvailableBalanceEgp);
        Assert.True(paid.Value.SubscriptionPaidThroughUtc > DateTime.UtcNow.AddDays(27));

        var platform = await harness.Db.Wallets.AsNoTracking()
            .SingleAsync(w => w.OwnerType == WalletOwnerType.Platform);
        Assert.Equal(1500m, platform.AvailableBalanceEgp);

        var extended = await wallets.PaySubscriptionMonthAsync(seeded.FactoryUserId, asFarm: false);
        Assert.True(extended.IsSuccess);
        Assert.Equal(0m, extended.Value!.AvailableBalanceEgp);
        Assert.True(extended.Value.SubscriptionPaidThroughUtc >
                    paid.Value.SubscriptionPaidThroughUtc!.Value.AddDays(27));
    }

    [Fact]
    public async Task PayMonth_Farm_DebitsFarmWallet()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFarmWalletAsync(harness.Db, available: 2000m);
        var wallets = CreateWalletService(harness.Db);

        var farm = await wallets.PaySubscriptionMonthAsync(seeded.FarmUserId, asFarm: true);
        Assert.True(farm.IsSuccess);
        Assert.True(farm.Value!.SubscriptionApplies);
        Assert.True(farm.Value.SubscriptionActive);
        Assert.Equal(800m, farm.Value.SubscriptionMonthlyEgp);
        Assert.Equal(1200m, farm.Value.AvailableBalanceEgp);
    }

    [Fact]
    public async Task PayMonth_InsufficientBalance_LeavesWalletUnchanged()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFactoryWalletAsync(harness.Db, available: 100m);
        var wallets = CreateWalletService(harness.Db);

        var paid = await wallets.PaySubscriptionMonthAsync(seeded.FactoryUserId, asFarm: false);
        Assert.True(paid.IsFailure);
        Assert.Equal(WalletErrors.SubscriptionInsufficient.Code, paid.Error!.Code);

        var wallet = await harness.Db.Wallets.AsNoTracking()
            .SingleAsync(w => w.OwnerId == seeded.FactoryId);
        Assert.Equal(100m, wallet.AvailableBalanceEgp);
        Assert.Null(wallet.SubscriptionPaidThroughUtc);
    }

    [Fact]
    public async Task StartTopUp_WithoutPaymobKeys_UsesSimulatorWhenAllowed()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFactoryWalletAsync(harness.Db, available: 0m);
        var wallets = CreateWalletService(
            harness.Db,
            new PaymobOptions { Enabled = true, AllowLocalSimulator = true });

        var session = await wallets.StartTopUpAsync(
            seeded.FactoryUserId, asFarm: false, amountEgp: 500, null, null);
        Assert.True(session.IsSuccess);
        Assert.Equal("Simulator", session.Value!.Mode);

        var credited = await wallets.CompleteSimulatorTopUpAsync(
            seeded.FactoryUserId, asFarm: false, session.Value.TopUpId);
        Assert.True(credited.IsSuccess);
        Assert.Equal(500m, credited.Value!.AvailableBalanceEgp);
    }

    [Fact]
    public async Task StartTopUp_WithoutPaymobKeys_FailsWhenSimulatorDisabled()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFactoryWalletAsync(harness.Db, available: 0m);
        var wallets = CreateWalletService(
            harness.Db,
            new PaymobOptions { Enabled = true, AllowLocalSimulator = false });

        var session = await wallets.StartTopUpAsync(
            seeded.FactoryUserId, asFarm: false, amountEgp: 500, null, null);
        Assert.True(session.IsFailure);
        Assert.Equal(WalletErrors.PaymobNotConfigured.Code, session.Error!.Code);
    }

    private static WalletService CreateWalletService(
        NileChainDbContext db,
        PaymobOptions? paymob = null) =>
        new(
            new WalletRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new UnitOfWork(db),
            new NoopPaymob(),
            Options.Create(new MockPaymentOptions()),
            Options.Create(paymob ?? new PaymobOptions()),
            NullLogger<WalletService>.Instance);

    private static async Task<(Guid FactoryId, Guid FactoryUserId)> SeedFactoryWalletAsync(
        NileChainDbContext db,
        decimal available)
    {
        var factoryUserId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = factoryUserId,
            UserName = "factory-sub@test.local",
            NormalizedUserName = "FACTORY-SUB@TEST.LOCAL",
            Email = "factory-sub@test.local",
            NormalizedEmail = "FACTORY-SUB@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });

        var factoryId = Guid.NewGuid();
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Subscription Factory",
            CreatedAt = DateTime.UtcNow
        });
        db.Wallets.Add(new Wallet
        {
            WalletId = Guid.NewGuid(),
            OwnerType = WalletOwnerType.Factory,
            OwnerId = factoryId,
            AvailableBalanceEgp = available,
            HeldBalanceEgp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1]
        });
        await db.SaveChangesAsync();
        return (factoryId, factoryUserId);
    }

    private static async Task<(Guid FarmId, Guid FarmUserId)> SeedFarmWalletAsync(
        NileChainDbContext db,
        decimal available)
    {
        var farmUserId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = farmUserId,
            UserName = "farm-sub@test.local",
            NormalizedUserName = "FARM-SUB@TEST.LOCAL",
            Email = "farm-sub@test.local",
            NormalizedEmail = "FARM-SUB@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });

        var farmId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Subscription Farm",
            CreatedAt = DateTime.UtcNow
        });
        db.Wallets.Add(new Wallet
        {
            WalletId = Guid.NewGuid(),
            OwnerType = WalletOwnerType.Farm,
            OwnerId = farmId,
            AvailableBalanceEgp = available,
            HeldBalanceEgp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1]
        });
        await db.SaveChangesAsync();
        return (farmId, farmUserId);
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
            var cs = $"Data Source=file:wallet-sub-{Guid.NewGuid():N}?mode=memory&cache=shared";
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
