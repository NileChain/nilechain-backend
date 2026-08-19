using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.AI.Plugins;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class HandoverHygieneTests
{
    [Fact]
    public void FarmCertificationRules_CountAdminGrantedUnexpiredOnly()
    {
        var now = DateTime.UtcNow;
        var leftover = new FarmCertification
        {
            GrantedByAdminUserId = null,
            ExpiresAt = now.AddYears(1)
        };
        var granted = new FarmCertification
        {
            GrantedByAdminUserId = Guid.NewGuid(),
            ExpiresAt = now.AddYears(1)
        };
        var expired = new FarmCertification
        {
            GrantedByAdminUserId = Guid.NewGuid(),
            ExpiresAt = now.AddDays(-1)
        };

        Assert.False(FarmCertificationRules.CountsTowardTrust(leftover, now));
        Assert.True(FarmCertificationRules.CountsTowardTrust(granted, now));
        Assert.False(FarmCertificationRules.CountsTowardTrust(expired, now));
    }

    [Fact]
    public void FactoryMayView_UnrelatedFarm_IsDenied()
    {
        Assert.False(FarmTrustVisibility.FactoryMayView(false, false));
        Assert.True(FarmTrustVisibility.FactoryMayView(true, false));
        Assert.True(FarmTrustVisibility.FactoryMayView(false, true));
    }

    [Fact]
    public void DisputeSla_IsOverdue_WhenPastDueAndOpen()
    {
        var created = DateTime.UtcNow.AddHours(-50);
        var open = new Dispute
        {
            Status = DisputeStatus.Open,
            CreatedAt = created,
            SlaDueAt = created.AddHours(48)
        };
        var resolved = new Dispute
        {
            Status = DisputeStatus.Resolved,
            CreatedAt = created,
            SlaDueAt = created.AddHours(48)
        };

        Assert.True(DisputeSla.IsOverdue(open, DateTime.UtcNow));
        Assert.False(DisputeSla.IsOverdue(resolved, DateTime.UtcNow));
    }

    [Fact]
    public async Task Farm_CannotAddOrDeleteCertification()
    {
        var sut = new FarmService(
            null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null!);

        var add = await sut.AddCertificationAsync(
            Guid.NewGuid(),
            new AddFarmCertificationRequest { CertificationId = Guid.NewGuid() });
        var del = await sut.DeleteCertificationAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(add.IsSuccess);
        Assert.Equal(FarmErrors.CertificationForbidden.Code, add.Error!.Code);
        Assert.False(del.IsSuccess);
        Assert.Equal(FarmErrors.CertificationForbidden.Code, del.Error!.Code);
    }

    [Fact]
    public async Task RiskScore_CountsAdminGrantedCerts_NotSelfServeLeftover()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFarmWithCertsAsync(harness.Db);

        var plugin = new RiskPlugin(harness.Db);
        var before = await plugin.CalculateRiskScore(seeded.FarmId);
        Assert.Equal(0m, before.CertificationScore);

        var grant = await CreateAdminService(harness.Db).GrantFarmCertificationAsync(
            seeded.AdminUserId,
            seeded.FarmId,
            new GrantFarmCertificationRequest
            {
                CertificationId = seeded.CatalogCertId,
                IssuedAt = DateTime.UtcNow.AddMonths(-1),
                ExpiresAt = DateTime.UtcNow.AddYears(1)
            });
        Assert.True(grant.IsSuccess);

        var after = await plugin.CalculateRiskScore(seeded.FarmId);
        Assert.Equal(12.5m, after.CertificationScore);
    }

    [Fact]
    public async Task Withdrawal_StaysPending_AdminCompleteAndReject()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedFarmWithWalletAsync(harness.Db, available: 400m);
        var wallets = CreateWalletService(harness.Db, instantDemo: false);

        var requested = await wallets.RequestWithdrawalAsync(
            seeded.FarmUserId, asFarm: true, 100m, "BankTransfer", "NBE-001");
        Assert.True(requested.IsSuccess);
        Assert.Equal(nameof(WalletWithdrawalStatus.Pending), requested.Value!.Status);

        var listed = await wallets.ListWithdrawalsForAdminAsync("Pending");
        Assert.Contains(listed.Value!.Items, w => w.WithdrawalId == requested.Value.WithdrawalId);

        var completed = await wallets.CompleteWithdrawalAsync(seeded.AdminUserId, requested.Value.WithdrawalId);
        Assert.True(completed.IsSuccess);
        Assert.Equal(nameof(WalletWithdrawalStatus.Completed), completed.Value!.Status);

        var second = await wallets.RequestWithdrawalAsync(
            seeded.FarmUserId, asFarm: true, 50m, "BankTransfer", "NBE-002");
        Assert.True(second.IsSuccess);
        var walletBeforeReject = await harness.Db.Wallets.AsNoTracking()
            .SingleAsync(w => w.OwnerId == seeded.FarmId);
        var availableBefore = walletBeforeReject.AvailableBalanceEgp;

        var rejected = await wallets.RejectWithdrawalAsync(
            seeded.AdminUserId, second.Value!.WithdrawalId, "insufficient docs");
        Assert.True(rejected.IsSuccess);
        Assert.Equal(nameof(WalletWithdrawalStatus.Cancelled), rejected.Value!.Status);

        var walletAfter = await harness.Db.Wallets.AsNoTracking()
            .SingleAsync(w => w.OwnerId == seeded.FarmId);
        Assert.Equal(availableBefore + 50m, walletAfter.AvailableBalanceEgp);
    }

    [Fact]
    public async Task Reviews_StripComment_ForStranger_FullTextForParty()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var review = new Review
        {
            ReviewId = Guid.NewGuid(),
            ContractId = seeded.ContractId,
            ReviewerId = seeded.FarmUserId,
            TargetId = seeded.FactoryUserId,
            Rating = 5,
            Comment = "secret party comment",
            CreatedAt = DateTime.UtcNow
        };
        harness.Db.Reviews.Add(review);
        await harness.Db.SaveChangesAsync();

        var svc = CreateReviewService(harness.Db);
        var party = await svc.GetReviewsForContractAsync(seeded.ContractId, seeded.FarmUserId, isAdmin: false);
        Assert.True(party.IsSuccess);
        Assert.Equal("secret party comment", party.Value!.Single().Comment);

        var stranger = await svc.GetReviewsForContractAsync(seeded.ContractId, Guid.NewGuid(), isAdmin: false);
        Assert.True(stranger.IsSuccess);
        Assert.Equal(5, stranger.Value!.Single().Rating);
        Assert.Null(stranger.Value.Single().Comment);
    }

    [Fact]
    public async Task DisputeDto_IsOverdue_AfterFortyEightHours()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateDisputeService(harness.Db);

        var opened = await service.OpenAsync(
            seeded.FarmUserId,
            seeded.ContractId,
            asFarm: true,
            "Other",
            "Late truck",
            evidenceFiles: null);
        Assert.True(opened.IsSuccess);
        Assert.False(opened.Value!.IsOverdue);
        Assert.NotNull(opened.Value.SlaDueAt);
        Assert.True(opened.Value.SlaDueAt!.Value > DateTime.UtcNow.AddHours(47));

        var entity = await harness.Db.Disputes.SingleAsync(d => d.DisputeId == opened.Value.DisputeId);
        entity.CreatedAt = DateTime.UtcNow.AddHours(-50);
        entity.SlaDueAt = entity.CreatedAt.AddHours(48);
        await harness.Db.SaveChangesAsync();

        var listed = await service.ListAdminAsync("Open", null, 1, 20);
        Assert.True(listed.IsSuccess);
        var dto = listed.Value!.Items.Single(d => d.DisputeId == opened.Value.DisputeId);
        Assert.True(dto.IsOverdue);
        Assert.Equal(dto.DisputeId, listed.Value.Items[0].DisputeId);
    }

    private static AdminService CreateAdminService(NileChainDbContext db) =>
        new(
            null!,
            null!,
            new FarmRepository(db),
            null!,
            null!,
            new Repository<Certification>(db),
            null!,
            null!,
            new UnitOfWork(db),
            null!);

    private static WalletService CreateWalletService(NileChainDbContext db, bool instantDemo) =>
        new(
            new WalletRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new UnitOfWork(db),
            new NoopPaymob(),
            Options.Create(new MockPaymentOptions { InstantDemoWithdrawals = instantDemo }),
            Options.Create(new PaymobOptions()),
            NullLogger<WalletService>.Instance);

    private static ReviewService CreateReviewService(NileChainDbContext db) =>
        new(
            new Repository<Review>(db),
            new Repository<Contract>(db),
            new Repository<FarmMatch>(db),
            new FarmRepository(db),
            new Repository<Factory>(db),
            new Repository<SupplyRequest>(db),
            new UnitOfWork(db));

    private static DisputeService CreateDisputeService(NileChainDbContext db) =>
        new(
            new DisputeRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Notification>(db),
            new NoopCloudinary(),
            new NileChain.Tests.TestDoubles.NoopEscrowPayments(),
            new UnitOfWork(db),
            Options.Create(new DisputeOptions { SlaHours = 48 }));

    private static async Task<(
        Guid FarmId,
        Guid AdminUserId,
        Guid CatalogCertId)> SeedFarmWithCertsAsync(NileChainDbContext db)
    {
        var farmUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        db.Users.AddRange(
            User(farmUserId, "farm-hygiene@test.local"),
            User(adminUserId, "admin-hygiene@test.local"));

        var farmId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Hygiene Farm",
            Governorate = "Qalyubia",
            CreatedAt = DateTime.UtcNow
        });

        var leftoverId = Guid.NewGuid();
        var catalogId = Guid.NewGuid();
        db.Certifications.AddRange(
            new Certification { CertificationId = leftoverId, Name = "SelfServeGAP" },
            new Certification { CertificationId = catalogId, Name = "GlobalGAP" });
        db.FarmCertifications.Add(new FarmCertification
        {
            FarmId = farmId,
            CertificationId = leftoverId,
            IssuedAt = DateTime.UtcNow.AddMonths(-2),
            ExpiresAt = DateTime.UtcNow.AddMonths(10),
            GrantedByAdminUserId = null
        });
        await db.SaveChangesAsync();
        return (farmId, adminUserId, catalogId);
    }

    private static async Task<(
        Guid FarmId,
        Guid FarmUserId,
        Guid AdminUserId)> SeedFarmWithWalletAsync(NileChainDbContext db, decimal available)
    {
        var farmUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        db.Users.AddRange(
            User(farmUserId, "farm-wd@test.local"),
            User(adminUserId, "admin-wd@test.local"));

        var farmId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Payout Farm",
            Governorate = "Giza",
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
        return (farmId, farmUserId, adminUserId);
    }

    private static async Task<(
        Guid ContractId,
        Guid FarmUserId,
        Guid FactoryUserId)> SeedSignedContractAsync(NileChainDbContext db)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();
        db.Users.AddRange(
            User(farmUserId, "farm-rev@test.local"),
            User(factoryUserId, "factory-rev@test.local"));

        var farmId = Guid.NewGuid();
        var factoryId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Review Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Review Factory",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });

        var cropId = Guid.NewGuid();
        db.CropTypes.Add(new CropType { CropTypeId = cropId, Name = "Tomato" });
        var requestId = Guid.NewGuid();
        db.SupplyRequests.Add(new SupplyRequest
        {
            RequestId = requestId,
            FactoryId = factoryId,
            CropTypeId = cropId,
            QuantityTons = 12,
            PricePerTon = 9000,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(8),
            Status = SupplyRequestStatus.Matched,
            CreatedAt = DateTime.UtcNow
        });
        var matchId = Guid.NewGuid();
        db.FarmMatches.Add(new FarmMatch
        {
            MatchId = matchId,
            RequestId = requestId,
            FarmId = farmId,
            Status = FarmMatchStatus.Accepted,
            MatchScore = 80,
            CreatedAt = DateTime.UtcNow
        });
        var contractId = Guid.NewGuid();
        var signedAt = DateTime.UtcNow;
        db.Contracts.Add(new Contract
        {
            ContractId = contractId,
            MatchId = matchId,
            Status = ContractStatus.Signed,
            GeneratedText = "body",
            FarmSignedAt = signedAt,
            FactorySignedAt = signedAt,
            SignedAt = signedAt,
            CreatedAt = signedAt,
            RowVersion = [1, 0, 0, 0, 0, 0, 0, 1]
        });
        await db.SaveChangesAsync();
        return (contractId, farmUserId, factoryUserId);
    }

    private static ApplicationUser User(Guid id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true,
        IsActive = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

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

    private sealed class NoopCloudinary : ICloudinaryService
    {
        public Task<(string Url, string PublicId)> UploadAsync(IFormFile file) =>
            Task.FromResult(("https://example.test/file", "public-id"));

        public Task DeleteAsync(string publicId) => Task.CompletedTask;
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
            var cs = $"Data Source=file:hygiene-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(cs);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(cs)
                .Options;
            var db = new HygieneDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class HygieneDbContext : NileChainDbContext
    {
        public HygieneDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

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
