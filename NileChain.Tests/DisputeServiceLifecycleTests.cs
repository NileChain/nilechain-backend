using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
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

public class DisputeServiceLifecycleTests
{
    [Fact]
    public async Task Open_ThenAdminResolve_NotifiesAndRecordsOutcome()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateDisputeService(harness.Db);

        var opened = await service.OpenAsync(
            seeded.FarmUserId,
            seeded.ContractId,
            asFarm: true,
            "QualityShortfall",
            "Moisture above agreed max",
            evidenceFiles: null);

        Assert.True(opened.IsSuccess);
        Assert.Equal("Open", opened.Value!.Status);
        Assert.True(opened.Value.FulfillmentFrozen);

        var underReview = await service.MoveToUnderReviewAsync(
            seeded.AdminUserId,
            opened.Value.DisputeId,
            "Checking evidence");
        Assert.True(underReview.IsSuccess);
        Assert.Equal("UnderReview", underReview.Value!.Status);

        var resolved = await service.ResolveAsync(
            seeded.AdminUserId,
            opened.Value.DisputeId,
            "Quality shortfall confirmed against sample photos",
            "Farm");
        Assert.True(resolved.IsSuccess);
        Assert.Equal("Resolved", resolved.Value!.Status);
        Assert.Equal("Farm", resolved.Value.OutcomeFavor);
        Assert.False(resolved.Value.FulfillmentFrozen);
        Assert.Contains(
            resolved.Value.Events,
            e => e.ToStatus == "Resolved" && e.Note != null && e.Note.Contains("favor of farm"));

        var notes = await harness.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Contains(notes, n => n.Type == "DisputeOpened");
        Assert.Contains(notes, n => n.Type == "DisputeUnderReview");
        Assert.Contains(notes, n => n.Type == "DisputeResolved");
    }

    [Fact]
    public async Task Open_OtherFarm_ReturnsContractNotFound()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);

        var otherFarmUserId = Guid.NewGuid();
        harness.Db.Users.Add(new ApplicationUser
        {
            Id = otherFarmUserId,
            UserName = "other-farm@test.local",
            NormalizedUserName = "OTHER-FARM@TEST.LOCAL",
            Email = "other-farm@test.local",
            NormalizedEmail = "OTHER-FARM@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        harness.Db.Farm.Add(new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = otherFarmUserId,
            Name = "Other Farm",
            Governorate = "Cairo",
            CreatedAt = DateTime.UtcNow
        });
        await harness.Db.SaveChangesAsync();

        var service = CreateDisputeService(harness.Db);
        var result = await service.OpenAsync(
            otherFarmUserId,
            seeded.ContractId,
            asFarm: true,
            "Other",
            "Should fail",
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(DisputeErrors.ContractNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Resolve_ByNonAdminPath_PartyCannotCallAdminTransition()
    {
        // Parties use Open/Get only — admin transitions require admin endpoints.
        // Guard: Resolve still works only when invoked; parties are blocked by controller roles.
        // Service-level: Resolve from Open is InvalidTransition (must go UnderReview first).
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateDisputeService(harness.Db);

        var opened = await service.OpenAsync(
            seeded.FactoryUserId,
            seeded.ContractId,
            asFarm: false,
            "LateDelivery",
            "Shipment delayed 5 days",
            null);
        Assert.True(opened.IsSuccess);

        var skipReview = await service.ResolveAsync(
            seeded.AdminUserId,
            opened.Value!.DisputeId,
            "Skipping review",
            "Factory");
        Assert.True(skipReview.IsFailure);
        Assert.Equal(DisputeErrors.InvalidTransition.Code, skipReview.Error!.Code);
    }

    [Fact]
    public async Task FulfillmentTransition_WhileDisputeOpen_ReturnsFrozenByDispute()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var disputes = CreateDisputeService(harness.Db);
        var fulfillment = CreateFulfillmentService(harness.Db);

        await fulfillment.EnsureCreatedForSignedContractAsync(seeded.ContractId, seeded.FarmUserId);
        var open = await disputes.OpenAsync(
            seeded.FarmUserId,
            seeded.ContractId,
            asFarm: true,
            "QuantityDispute",
            "Short by 2 tons",
            null);
        Assert.True(open.IsSuccess);

        var ship = await fulfillment.MarkShippedAsync(seeded.FarmUserId, seeded.ContractId);
        Assert.True(ship.IsFailure);
        Assert.Equal(FulfillmentErrors.FrozenByDispute.Code, ship.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(ship.Error));
    }

    [Fact]
    public async Task PaymentTransition_WhileDisputeOpen_ReturnsFrozenByDispute()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var disputes = CreateDisputeService(harness.Db);
        var payments = CreatePaymentService(harness.Db);

        var supply = await harness.Db.SupplyRequests.SingleAsync();
        await payments.EnsureCreatedForSignedContractAsync(seeded.ContractId, seeded.FactoryUserId, supply);

        var open = await disputes.OpenAsync(
            seeded.FactoryUserId,
            seeded.ContractId,
            asFarm: false,
            "Other",
            "Invoice mismatch",
            null);
        Assert.True(open.IsSuccess);

        var milestone = await harness.Db.Transactions
            .AsNoTracking()
            .Where(t => t.ContractId == seeded.ContractId && t.Status == TransactionStatus.Pending)
            .OrderBy(t => t.Sequence)
            .FirstAsync();

        var markPaid = await payments.MarkPaidAsync(
            seeded.FactoryUserId,
            seeded.ContractId,
            milestone.TransactionId);
        Assert.True(markPaid.IsFailure);
        Assert.Equal(PaymentMilestoneErrors.FrozenByDispute.Code, markPaid.Error!.Code);
    }

    [Fact]
    public async Task HasActiveDispute_BlocksRegenPolicyCheck()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateDisputeService(harness.Db);

        Assert.False(await service.HasActiveDisputeAsync(seeded.ContractId));

        var open = await service.OpenAsync(
            seeded.FarmUserId,
            seeded.ContractId,
            asFarm: true,
            "Other",
            "Block regen",
            null);
        Assert.True(open.IsSuccess);
        Assert.True(await service.HasActiveDisputeAsync(seeded.ContractId));

        await service.MoveToUnderReviewAsync(seeded.AdminUserId, open.Value!.DisputeId, null);
        Assert.True(await service.HasActiveDisputeAsync(seeded.ContractId));

        await service.RejectAsync(seeded.AdminUserId, open.Value.DisputeId, "Insufficient evidence");
        Assert.False(await service.HasActiveDisputeAsync(seeded.ContractId));
    }

    [Fact]
    public async Task SecondOpen_WhileActive_ReturnsActiveExists()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateDisputeService(harness.Db);

        Assert.True((await service.OpenAsync(
            seeded.FarmUserId, seeded.ContractId, true, "Other", "First", null)).IsSuccess);

        var second = await service.OpenAsync(
            seeded.FactoryUserId, seeded.ContractId, false, "Other", "Second", null);
        Assert.True(second.IsFailure);
        Assert.Equal(DisputeErrors.ActiveExists.Code, second.Error!.Code);
    }

    private static DisputeService CreateDisputeService(NileChainDbContext db) =>
        new(
            new DisputeRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Notification>(db),
            new NoopCloudinary(),
            new NileChain.Tests.TestDoubles.NoopEscrowPayments(),
            new UnitOfWork(db));

    private static FulfillmentService CreateFulfillmentService(NileChainDbContext db) =>
        new(
            new FulfillmentRepository(db),
            new DisputeRepository(db),
            new PaymentMilestoneRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Contract>(db),
            new Repository<Notification>(db),
            new Repository<SupplyRequest>(db),
            new NileChain.Tests.TestDoubles.NoopEscrowPayments(),
            new UnitOfWork(db));

    private static PaymentMilestoneService CreatePaymentService(NileChainDbContext db) =>
        new(
            new PaymentMilestoneRepository(db),
            new EscrowTransactionRepository(db),
            new DisputeRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Contract>(db),
            new Repository<Notification>(db),
            new UnitOfWork(db),
            Options.Create(new PaymentMilestoneOptions()),
            Options.Create(new MockPaymentOptions { MockGatewayEnabled = false }),
            NullLogger<PaymentMilestoneService>.Instance,
            new NoopCloudinary());

    private static async Task<(
        Guid ContractId,
        Guid FarmUserId,
        Guid FactoryUserId,
        Guid AdminUserId)> SeedSignedContractAsync(NileChainDbContext db)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        db.Users.AddRange(
            new ApplicationUser
            {
                Id = farmUserId,
                UserName = "farm-d@test.local",
                NormalizedUserName = "FARM-D@TEST.LOCAL",
                Email = "farm-d@test.local",
                NormalizedEmail = "FARM-D@TEST.LOCAL",
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString()
            },
            new ApplicationUser
            {
                Id = factoryUserId,
                UserName = "factory-d@test.local",
                NormalizedUserName = "FACTORY-D@TEST.LOCAL",
                Email = "factory-d@test.local",
                NormalizedEmail = "FACTORY-D@TEST.LOCAL",
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString()
            },
            new ApplicationUser
            {
                Id = adminUserId,
                UserName = "admin-d@test.local",
                NormalizedUserName = "ADMIN-D@TEST.LOCAL",
                Email = "admin-d@test.local",
                NormalizedEmail = "ADMIN-D@TEST.LOCAL",
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString()
            });

        var farmId = Guid.NewGuid();
        var factoryId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Dispute Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Dispute Factory",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });

        var cropId = Guid.NewGuid();
        db.CropTypes.Add(new CropType { CropTypeId = cropId, Name = "Wheat" });

        var requestId = Guid.NewGuid();
        db.SupplyRequests.Add(new SupplyRequest
        {
            RequestId = requestId,
            FactoryId = factoryId,
            CropTypeId = cropId,
            QuantityTons = 10,
            PricePerTon = 1000,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(10),
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
            RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 1 }
        });

        await db.SaveChangesAsync();
        return (contractId, farmUserId, factoryUserId, adminUserId);
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
        private readonly string _connectionString;

        private SqliteHarness(SqliteConnection keepAlive, string connectionString, NileChainDbContext db)
        {
            _keepAlive = keepAlive;
            _connectionString = connectionString;
            Db = db;
        }

        public NileChainDbContext Db { get; }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connectionString = $"Data Source=file:disp-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();

            var db = Open(connectionString);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, connectionString, db);
        }

        private static NileChainDbContext Open(string connectionString)
        {
            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new SqliteLifecycleDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class SqliteLifecycleDbContext : NileChainDbContext
    {
        public SqliteLifecycleDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<FarmMatch>()
                .Property(m => m.EligibilitySnapshotJson)
                .HasColumnType("TEXT");
        }
    }
}
