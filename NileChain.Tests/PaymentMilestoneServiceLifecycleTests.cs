using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Errors;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class PaymentMilestoneServiceLifecycleTests
{
    [Fact]
    public async Task EnsureCreated_Idempotent_AndComputesAmountsFromQtyTimesPrice()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, _, supply) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);

        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId, supply);
        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId, supply);

        var rows = await harness.Db.Transactions
            .Where(t => t.ContractId == contractId)
            .OrderBy(t => t.Sequence)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(3000m, rows[0].Amount); // 30% of 10*1000
        Assert.Equal(7000m, rows[1].Amount);
        Assert.All(rows, r => Assert.Equal(TransactionStatus.Pending, r.Status));
        Assert.Equal(2, await harness.Db.TransactionEvents.CountAsync());
    }

    [Fact]
    public async Task EnsureCreated_MissingPrice_DoesNotCreateSilentZeroAmounts()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, _, supply) = await SeedSignedContractAsync(harness.Db, pricePerTon: null);
        var service = CreateService(harness.Db);

        await service.EnsureCreatedForSignedContractAsync(contractId, farmUserId, supply);

        Assert.Equal(0, await harness.Db.Transactions.CountAsync(t => t.ContractId == contractId));
    }

    [Fact]
    public async Task MarkPaid_FactoryOnly_ConfirmReceived_FarmOnly()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);

        var deposit = await harness.Db.Transactions
            .AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var farmCannotMark = await service.MarkPaidAsync(farmUserId, contractId, deposit.TransactionId);
        Assert.True(farmCannotMark.IsFailure);
        Assert.Equal(PaymentMilestoneErrors.Forbidden.Code, farmCannotMark.Error!.Code);

        var marked = await service.MarkPaidAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(marked.IsSuccess);
        Assert.Equal("MarkedPaid", marked.Value!.Milestones.Single(m => m.Sequence == 1).Status);

        var factoryCannotConfirm = await service.ConfirmReceivedAsync(
            factoryUserId, contractId, deposit.TransactionId);
        Assert.True(factoryCannotConfirm.IsFailure);
        Assert.Equal(PaymentMilestoneErrors.Forbidden.Code, factoryCannotConfirm.Error!.Code);

        var confirmed = await service.ConfirmReceivedAsync(farmUserId, contractId, deposit.TransactionId);
        Assert.True(confirmed.IsSuccess);
        Assert.Equal("Completed", confirmed.Value!.Milestones.Single(m => m.Sequence == 1).Status);

        Assert.True(await harness.Db.Notifications.AnyAsync(n => n.Type == "PaymentMarked"));
        Assert.True(await harness.Db.Notifications.AnyAsync(n => n.Type == "PaymentReceived"));
    }

    [Fact]
    public async Task VoidForContract_VoidsIncludingCompleted_ThenEnsureRecreatesNewGeneration()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var service = CreateService(harness.Db);
        await service.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);

        var deposit = await harness.Db.Transactions
            .AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);
        await service.MarkPaidAsync(factoryUserId, contractId, deposit.TransactionId);
        await service.ConfirmReceivedAsync(farmUserId, contractId, deposit.TransactionId);

        // High-stakes: farm already confirmed receipt on deposit before regen voids the schedule.
        var completedBeforeVoid = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.TransactionId == deposit.TransactionId);
        Assert.Equal(TransactionStatus.Completed, completedBeforeVoid.Status);

        await service.VoidForContractAsync(
            contractId,
            factoryUserId,
            "Contract text regenerated — prior payment milestone schedule voided");

        var afterVoid = await harness.Db.Transactions
            .AsNoTracking()
            .Where(t => t.ContractId == contractId)
            .ToListAsync();
        Assert.Equal(2, afterVoid.Count);
        Assert.All(afterVoid, t => Assert.Equal(TransactionStatus.Voided, t.Status));

        // Audit must preserve prior confirmed state (Completed → Voided), not only "Voided".
        var completedVoidEvent = await harness.Db.TransactionEvents.AsNoTracking()
            .SingleAsync(e =>
                e.TransactionId == deposit.TransactionId
                && e.ToStatus == TransactionStatus.Voided);
        Assert.Equal(TransactionStatus.Completed, completedVoidEvent.FromStatus);
        Assert.Contains("regenerated", completedVoidEvent.Note, StringComparison.OrdinalIgnoreCase);

        // Pending milestone (On delivery) voided from Pending — still audited with prior status.
        var onDelivery = afterVoid.Single(t => t.Sequence == 2);
        var pendingVoidEvent = await harness.Db.TransactionEvents.AsNoTracking()
            .SingleAsync(e =>
                e.TransactionId == onDelivery.TransactionId
                && e.ToStatus == TransactionStatus.Voided);
        Assert.Equal(TransactionStatus.Pending, pendingVoidEvent.FromStatus);

        // Re-sign path would call Ensure again — recreate with generation 2 and fresh amounts.
        supply.PricePerTon = 2000; // regen changed price
        harness.Db.SupplyRequests.Update(supply);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        await service.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);

        var active = await harness.Db.Transactions
            .AsNoTracking()
            .Where(t => t.ContractId == contractId && t.Status != TransactionStatus.Voided)
            .OrderBy(t => t.Sequence)
            .ToListAsync();

        Assert.Equal(2, active.Count);
        Assert.Equal(2, active[0].ScheduleGeneration);
        Assert.Equal(6000m, active[0].Amount); // 30% of 10*2000
        Assert.Equal(14000m, active[1].Amount);

        // Prior Completed→Voided audit row remains on the voided generation (dispute trail).
        Assert.True(await harness.Db.TransactionEvents.AsNoTracking().AnyAsync(e =>
            e.TransactionId == deposit.TransactionId
            && e.FromStatus == TransactionStatus.Completed
            && e.ToStatus == TransactionStatus.Voided));
    }

    private static PaymentMilestoneService CreateService(NileChainDbContext db) =>
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

    private sealed class NoopCloudinary : NileChain.Application.Interfaces.ICloudinaryService
    {
        public Task<(string Url, string PublicId)> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file) =>
            Task.FromResult(("https://example.test/receipt", "receipt-public-id"));

        public Task DeleteAsync(string publicId) => Task.CompletedTask;
    }

    private static async Task<(Guid ContractId, Guid FarmUserId, Guid FactoryUserId, SupplyRequest Supply)>
        SeedSignedContractAsync(NileChainDbContext db, decimal? pricePerTon = 1000m)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();

        db.Users.Add(new ApplicationUser
        {
            Id = farmUserId,
            UserName = "farm-pay@test.local",
            NormalizedUserName = "FARM-PAY@TEST.LOCAL",
            Email = "farm-pay@test.local",
            NormalizedEmail = "FARM-PAY@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        db.Users.Add(new ApplicationUser
        {
            Id = factoryUserId,
            UserName = "factory-pay@test.local",
            NormalizedUserName = "FACTORY-PAY@TEST.LOCAL",
            Email = "factory-pay@test.local",
            NormalizedEmail = "FACTORY-PAY@TEST.LOCAL",
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
            Name = "Pay Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Pay Factory",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });

        var cropId = Guid.NewGuid();
        db.CropTypes.Add(new CropType { CropTypeId = cropId, Name = "Wheat" });

        var requestId = Guid.NewGuid();
        var supply = new SupplyRequest
        {
            RequestId = requestId,
            FactoryId = factoryId,
            CropTypeId = cropId,
            QuantityTons = 10,
            PricePerTon = pricePerTon,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(10),
            Status = SupplyRequestStatus.Matched,
            CreatedAt = DateTime.UtcNow
        };
        db.SupplyRequests.Add(supply);

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
        return (contractId, farmUserId, factoryUserId, supply);
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
            var connectionString = $"Data Source=file:pay-{Guid.NewGuid():N}?mode=memory&cache=shared";
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
            // SQL Server nvarchar(max) is invalid DDL for SQLite EnsureCreated.
            modelBuilder.Entity<FarmMatch>()
                .Property(m => m.EligibilitySnapshotJson)
                .HasColumnType("TEXT");
        }
    }
}
