using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Services;
using NileChain.Application.Dtos.Fulfillment;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

/// <summary>
/// Explicit lifecycle coverage beyond the transition matrix:
/// ensure-create race/idempotency, void-on-regen, and Conflict on stale transition.
/// </summary>
public class FulfillmentServiceLifecycleTests
{
    [Fact]
    public async Task EnsureCreated_CalledTwiceSequentially_ReturnsSameFulfillmentWithoutThrowing()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, _) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);

        var ex = await Record.ExceptionAsync(async () =>
        {
            await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId, DateTime.UtcNow.Date.AddDays(7));
            await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId, DateTime.UtcNow.Date.AddDays(7));
        });
        Assert.Null(ex);

        var count = await harness.Db.Fulfillments.CountAsync(f => f.ContractId == contractId);
        Assert.Equal(1, count);

        var fulfillment = await harness.Db.Fulfillments.SingleAsync(f => f.ContractId == contractId);
        Assert.Equal(FulfillmentStatus.Planned, fulfillment.Status);
    }

    [Fact]
    public async Task EnsureCreated_CalledConcurrently_LeavesSingleFulfillmentWithoutThrowing()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);

        // DbContext is not thread-safe — each concurrent call gets its own context on the shared DB.
        var tasks = Enumerable.Range(0, 8).Select(async i =>
        {
            await using var db = harness.OpenContext();
            var service = CreateService(db);
            await service.EnsureCreatedForSignedContractAsync(
                contractId,
                i % 2 == 0 ? farmUserId : factoryUserId,
                DateTime.UtcNow.Date.AddDays(3));
        });

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);

        var count = await harness.Db.Fulfillments.CountAsync(f => f.ContractId == contractId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task VoidForContract_AfterSigning_VoidsExistingFulfillment_AsRegenWould()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);

        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        var before = await harness.Db.Fulfillments.AsNoTracking().SingleAsync(f => f.ContractId == contractId);
        Assert.Equal(FulfillmentStatus.Planned, before.Status);

        // Same reason FactoryService / AI agent regen paths pass after text replace.
        await service.VoidForContractAsync(
            contractId,
            factoryUserId,
            "Contract text regenerated — prior fulfillment voided");

        var after = await harness.Db.Fulfillments.AsNoTracking().SingleAsync(f => f.ContractId == contractId);
        Assert.Equal(FulfillmentStatus.Voided, after.Status);
        Assert.NotNull(after.VoidedAt);

        var voidEvent = await harness.Db.FulfillmentEvents
            .Where(e => e.FulfillmentId == after.FulfillmentId && e.ToStatus == FulfillmentStatus.Voided)
            .SingleAsync();
        Assert.Contains("regenerated", voidEvent.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transition_WhenCurrentStatusUnexpected_ReturnsFulfillmentConflict()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, _) = await SeedSignedContractAsync(harness.Db);
        var repo = new FulfillmentRepository(harness.Db);
        var service = CreateService(harness.Db);

        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        var fulfillment = await harness.Db.Fulfillments.AsNoTracking().SingleAsync(f => f.ContractId == contractId);

        // Winner updates Planned → Shipped.
        Assert.True(await repo.TryAtomicTransitionAsync(
            fulfillment.FulfillmentId,
            FulfillmentStatus.Planned,
            FulfillmentStatus.Shipped,
            DateTime.UtcNow));

        // Stale attempt still expects Planned → 0 rows.
        Assert.False(await repo.TryAtomicTransitionAsync(
            fulfillment.FulfillmentId,
            FulfillmentStatus.Planned,
            FulfillmentStatus.Shipped,
            DateTime.UtcNow));

        // Service maps atomic miss to Fulfillment.Conflict (simulates race: read Planned, then status changed).
        await using var harness2 = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness2.Db);
        await CreateService(harness2.Db).EnsureCreatedForSignedContractAsync(seeded.ContractId, seeded.FarmUserId);

        var raceService = CreateService(
            harness2.Db,
            new StaleTransitionFulfillmentRepository(harness2.Db));

        var result = await raceService.MarkShippedAsync(seeded.FarmUserId, seeded.ContractId);
        Assert.True(result.IsFailure);
        Assert.Equal(FulfillmentErrors.Conflict.Code, result.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(result.Error));
    }

    [Fact]
    public async Task Receive_WithoutWeight_ReturnsWeighbridgeRequired()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        await service.MarkShippedAsync(farmUserId, contractId);

        var missing = await service.MarkReceivedAsync(factoryUserId, contractId, null);
        Assert.True(missing.IsFailure);
        Assert.Equal(FulfillmentErrors.WeighbridgeRequired.Code, missing.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(missing.Error));

        var zero = await service.MarkReceivedAsync(
            factoryUserId, contractId, new() { WeighedQuantityTons = 0 });
        Assert.True(zero.IsFailure);
        Assert.Equal(FulfillmentErrors.WeighbridgeRequired.Code, zero.Error!.Code);
    }

    [Fact]
    public async Task Receive_Shortage_ScalesAllOpenMilestones()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        await SeedOpenMilestonesAsync(harness.Db, contractId, 3000m, 7000m);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        await service.MarkShippedAsync(farmUserId, contractId);

        var received = await service.MarkReceivedAsync(
            factoryUserId,
            contractId,
            new() { WeighedQuantityTons = 9.4m });
        Assert.True(received.IsSuccess);
        Assert.Equal(FulfillmentStatus.Received.ToString(), received.Value!.Status);
        Assert.Equal(9.4m, received.Value.WeighedQuantityTons);

        var rows = await harness.Db.Transactions.AsNoTracking()
            .Where(t => t.ContractId == contractId)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(2820m, rows[0].Amount); // 3000 * 0.94
        Assert.Equal(6580m, rows[1].Amount); // 7000 * 0.94
    }

    [Fact]
    public async Task Receive_Overage_DoesNotIncreasePayable()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        await SeedOpenMilestonesAsync(harness.Db, contractId, 3000m, 7000m);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        await service.MarkShippedAsync(farmUserId, contractId);

        var received = await service.MarkReceivedAsync(
            factoryUserId, contractId, new() { WeighedQuantityTons = 12m });
        Assert.True(received.IsSuccess);

        var rows = await harness.Db.Transactions.AsNoTracking()
            .Where(t => t.ContractId == contractId)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
        Assert.Equal(3000m, rows[0].Amount);
        Assert.Equal(7000m, rows[1].Amount);
    }

    [Fact]
    public async Task RejectAtGate_FactoryGate_ReturnFreightIsFarm()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(
            harness.Db, DeliveryPoint.FactoryGate);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(
            contractId, farmUserId, null, DeliveryPoint.FactoryGate, DealParty.Farm, DealParty.Farm);
        await service.MarkShippedAsync(farmUserId, contractId);

        var rejected = await service.MarkRejectedAtGateAsync(
            factoryUserId, contractId, new() { Reason = "QualityFail" });
        Assert.True(rejected.IsSuccess);
        Assert.Equal(FulfillmentStatus.RejectedAtGate.ToString(), rejected.Value!.Status);
        Assert.Equal(DealParty.Farm.ToString(), rejected.Value.ReturnFreightBearer);

        var farmAttempt = await service.MarkRejectedAtGateAsync(
            farmUserId, contractId, new() { Reason = "QualityFail" });
        Assert.True(farmAttempt.IsFailure);
        Assert.Equal(FactoryErrors.FactoryNotFound.Code, farmAttempt.Error!.Code);
    }

    [Fact]
    public async Task RejectAtGate_FarmGate_ReturnFreightIsFactory()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(
            harness.Db, DeliveryPoint.FarmGate);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(
            contractId, farmUserId, null, DeliveryPoint.FarmGate, DealParty.Factory, DealParty.Factory);
        await service.MarkShippedAsync(farmUserId, contractId);

        var rejected = await service.MarkRejectedAtGateAsync(
            factoryUserId, contractId, new() { Reason = "WrongCrop" });
        Assert.True(rejected.IsSuccess);
        Assert.Equal(DealParty.Factory.ToString(), rejected.Value!.ReturnFreightBearer);
    }

    [Fact]
    public async Task RejectAtGate_AfterReceived_InvalidTransition()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        await service.MarkShippedAsync(farmUserId, contractId);
        await service.MarkReceivedAsync(
            factoryUserId, contractId, new() { WeighedQuantityTons = 10m });

        var rejected = await service.MarkRejectedAtGateAsync(
            factoryUserId, contractId, new() { Reason = "QualityFail" });
        Assert.True(rejected.IsFailure);
        Assert.Equal(FulfillmentErrors.InvalidTransition.Code, rejected.Error!.Code);
    }

    [Fact]
    public async Task QualityCheck_AfterWeighShortage_DoesNotDoubleCountWeight()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        await SeedOpenMilestonesAsync(harness.Db, contractId, 3000m, 7000m);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        await service.MarkShippedAsync(farmUserId, contractId);
        await service.MarkReceivedAsync(
            factoryUserId, contractId, new() { WeighedQuantityTons = 9.4m });

        var qc = await service.MarkQualityCheckedAsync(
            factoryUserId,
            contractId,
            new() { AcceptedQuantityTons = 9.4m, DiscountPercent = 0 });
        Assert.True(qc.IsSuccess);

        var rows = await harness.Db.Transactions.AsNoTracking()
            .Where(t => t.ContractId == contractId)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
        Assert.Equal(2820m, rows[0].Amount);
        Assert.Equal(6580m, rows[1].Amount);
    }

    [Fact]
    public async Task QualityCheck_AcceptedExceedsWeighed_Conflict()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId);
        await service.MarkShippedAsync(farmUserId, contractId);
        await service.MarkReceivedAsync(
            factoryUserId, contractId, new() { WeighedQuantityTons = 8m });

        var qc = await service.MarkQualityCheckedAsync(
            factoryUserId,
            contractId,
            new() { AcceptedQuantityTons = 9m });
        Assert.True(qc.IsFailure);
        Assert.Equal(FulfillmentErrors.AcceptedExceedsWeighed.Code, qc.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(qc.Error));
    }

    [Fact]
    public void EligibilityChanged_MapsToHttpConflict()
    {
        Assert.Equal(
            HttpStatusCode.Conflict,
            ResultHttpMapper.MapStatus(FactoryErrors.EligibilityChanged));
    }

    private static FulfillmentService CreateService(
        NileChainDbContext db,
        IFulfillmentRepository? fulfillments = null) =>
        new(
            fulfillments ?? new FulfillmentRepository(db),
            new DisputeRepository(db),
            new PaymentMilestoneRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Contract>(db),
            new Repository<Notification>(db),
            new Repository<SupplyRequest>(db),
            new NileChain.Tests.TestDoubles.NoopEscrowPayments(),
            new UnitOfWork(db));

    private static async Task SeedOpenMilestonesAsync(
        NileChainDbContext db, Guid contractId, decimal deposit, decimal onDelivery)
    {
        db.Transactions.AddRange(
            new Transaction
            {
                TransactionId = Guid.NewGuid(),
                ContractId = contractId,
                ScheduleGeneration = 1,
                Sequence = 1,
                Label = "Deposit",
                PaymentMethod = "Deposit",
                Percent = 30,
                Amount = deposit,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            },
            new Transaction
            {
                TransactionId = Guid.NewGuid(),
                ContractId = contractId,
                ScheduleGeneration = 1,
                Sequence = 2,
                Label = "On delivery",
                PaymentMethod = "OnDelivery",
                Percent = 70,
                Amount = onDelivery,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid ContractId, Guid FarmUserId, Guid FactoryUserId)> SeedSignedContractAsync(
        NileChainDbContext db,
        DeliveryPoint deliveryPoint = DeliveryPoint.FactoryGate)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();

        db.Users.Add(new ApplicationUser
        {
            Id = farmUserId,
            UserName = "farm@test.local",
            NormalizedUserName = "FARM@TEST.LOCAL",
            Email = "farm@test.local",
            NormalizedEmail = "FARM@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        db.Users.Add(new ApplicationUser
        {
            Id = factoryUserId,
            UserName = "factory@test.local",
            NormalizedUserName = "FACTORY@TEST.LOCAL",
            Email = "factory@test.local",
            NormalizedEmail = "FACTORY@TEST.LOCAL",
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
            Name = "Test Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Test Factory",
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
            DeliveryPoint = deliveryPoint,
            FreightPayer = DeliveryTermsPolicy.DefaultFreight(deliveryPoint),
            TransitRisk = DeliveryTermsPolicy.DefaultTransit(deliveryPoint),
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
        return (contractId, farmUserId, factoryUserId);
    }

    /// <summary>
    /// Reads real Planned fulfillment, but atomic update always loses — models unexpected current status.
    /// </summary>
    private sealed class StaleTransitionFulfillmentRepository : IFulfillmentRepository
    {
        private readonly FulfillmentRepository _inner;

        public StaleTransitionFulfillmentRepository(NileChainDbContext db) =>
            _inner = new FulfillmentRepository(db);

        public Task<Fulfillment?> GetByContractIdAsync(Guid contractId, bool includeEvents = true) =>
            _inner.GetByContractIdAsync(contractId, includeEvents);

        public Task<Fulfillment?> GetByIdAsync(Guid fulfillmentId) =>
            _inner.GetByIdAsync(fulfillmentId);

        public Task AddAsync(Fulfillment fulfillment) => _inner.AddAsync(fulfillment);

        public Task AddEventAsync(FulfillmentEvent fulfillmentEvent) =>
            _inner.AddEventAsync(fulfillmentEvent);

        public Task<bool> TryAtomicTransitionAsync(
            Guid fulfillmentId,
            FulfillmentStatus expectedFrom,
            FulfillmentStatus to,
            DateTime utcNow,
            string? qualityNotes = null,
            bool requireNoActiveDispute = false,
            string? carrier = null,
            string? trackingNumber = null,
            string? shippedNotes = null,
            decimal? acceptedQuantityTons = null,
            decimal? discountPercent = null,
            bool? specsMet = null,
            string? specsOutcomeNotes = null,
            decimal? weighedQuantityTons = null,
            string? weighbridgeTicketUrl = null,
            GateRejectReason? gateRejectReason = null,
            string? gateRejectNotes = null,
            DealParty? returnFreightBearer = null) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Fulfillment>> GetStuckPlannedAsync(DateTime asOfUtcNoon, int skip, int take) =>
            _inner.GetStuckPlannedAsync(asOfUtcNoon, skip, take);

        public Task<int> CountStuckPlannedAsync(DateTime asOfUtcNoon) =>
            _inner.CountStuckPlannedAsync(asOfUtcNoon);
    }

    /// <summary>
    /// Shared in-memory SQLite with RowVersion usable on insert (SQL Server rowversion is DB-generated).
    /// </summary>
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
            var connectionString = $"Data Source=file:fulf-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();

            var db = Open(connectionString);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, connectionString, db);
        }

        public NileChainDbContext OpenContext() => Open(_connectionString);

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

            // IsRowVersion omits the column on INSERT; SQLite cannot auto-fill it.
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
