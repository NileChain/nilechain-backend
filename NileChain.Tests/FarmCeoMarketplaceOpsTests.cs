using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Tests;

public class FarmCeoMarketplaceOpsTests
{
    [Fact]
    public async Task QcDiscount_ReducesFirstOpenMilestoneAmount()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedAsync(harness.Db);

        var milestones = new PaymentMilestoneRepository(harness.Db);
        var txId = Guid.NewGuid();
        await milestones.AddRangeAsync(
        [
            new Transaction
            {
                TransactionId = txId,
                ContractId = seeded.ContractId,
                ScheduleGeneration = 1,
                Sequence = 1,
                Label = "Deposit",
                PaymentMethod = "deposit",
                Percent = 30,
                Amount = 1000m,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            },
            new Transaction
            {
                TransactionId = Guid.NewGuid(),
                ContractId = seeded.ContractId,
                ScheduleGeneration = 1,
                Sequence = 2,
                Label = "Balance",
                PaymentMethod = "balance",
                Percent = 70,
                Amount = 2000m,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }
        ]);
        await harness.Db.SaveChangesAsync();

        var result = await milestones.TryApplyDiscountToFirstOpenMilestoneAsync(
            seeded.ContractId,
            10m,
            seeded.FactoryUserId,
            DateTime.UtcNow);
        await harness.Db.SaveChangesAsync();

        Assert.Equal(txId, result.TransactionId);
        Assert.Equal(1000m, result.PreviousAmount);
        Assert.Equal(900m, result.NewAmount);

        var updated = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.TransactionId == txId);
        Assert.Equal(900m, updated.Amount);
    }

    [Fact]
    public async Task ReminderPass_CreatesOverdueAndCertNotifications()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        await SeedAsync(harness.Db, withCert: true, overdueTx: true);

        var (overdue, certs) = await NileChain.API.HostedServices.FarmMarketplaceReminderHostedService
            .RunReminderPassAsync(harness.Db, DateTime.UtcNow);

        Assert.True(overdue >= 1);
        Assert.True(certs >= 1);
        var notes = await harness.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Contains(notes, n => n.Type == "PaymentOverdue");
        Assert.Contains(notes, n => n.Type == "CertExpiring");
    }

    private static async Task<(Guid ContractId, Guid FarmUserId, Guid FactoryUserId)> SeedAsync(
        NileChainDbContext db,
        bool withCert = false,
        bool overdueTx = false)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = farmUserId,
            UserName = $"farm-{farmUserId:N}@test.local",
            NormalizedUserName = $"FARM-{farmUserId:N}@TEST.LOCAL",
            Email = $"farm-{farmUserId:N}@test.local",
            NormalizedEmail = $"FARM-{farmUserId:N}@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        db.Users.Add(new ApplicationUser
        {
            Id = factoryUserId,
            UserName = $"factory-{factoryUserId:N}@test.local",
            NormalizedUserName = $"FACTORY-{factoryUserId:N}@TEST.LOCAL",
            Email = $"factory-{factoryUserId:N}@test.local",
            NormalizedEmail = $"FACTORY-{factoryUserId:N}@TEST.LOCAL",
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
            Name = "Ops Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Ops Factory",
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
            DeliveryDate = DateTime.UtcNow.Date.AddDays(14),
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

        if (withCert)
        {
            var certId = Guid.NewGuid();
            db.Certifications.Add(new Certification
            {
                CertificationId = certId,
                Name = "GlobalGAP"
            });
            db.FarmCertifications.Add(new FarmCertification
            {
                FarmId = farmId,
                CertificationId = certId,
                IssuedAt = DateTime.UtcNow.AddMonths(-11),
                ExpiresAt = DateTime.UtcNow.AddDays(10)
            });
        }

        if (overdueTx)
        {
            db.Transactions.Add(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                ContractId = contractId,
                ScheduleGeneration = 1,
                Sequence = 1,
                Label = "Deposit",
                PaymentMethod = "deposit",
                Percent = 30,
                Amount = 500,
                Status = TransactionStatus.Pending,
                DueDate = DateTime.UtcNow.Date.AddDays(-3),
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return (contractId, farmUserId, factoryUserId);
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
            var cs = $"Data Source=file:ops-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(cs);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(cs)
                .Options;
            var db = new OpsDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class OpsDbContext : NileChainDbContext
    {
        public OpsDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsRowVersion()
                .HasDefaultValue(new byte[] { 1 });
        }
    }
}
