using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Services;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class ContractIntegrityChainTests
{
    [Fact]
    public void Hasher_IsDeterministic_AndChangesWithText()
    {
        var signedAt = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        var contractId = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var a = new ContractIntegrityPayload(
            contractId, "Nile Farm", "Delta Factory", "Wheat", 10m, 12000m,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            "عقد توريد", signedAt, signedAt);
        var b = a with { GeneratedText = "عقد توريد معدل" };

        var ha = ContractIntegrityHasher.ComputeContentHash(a);
        var hb = ContractIntegrityHasher.ComputeContentHash(b);

        Assert.Equal(64, ha.Length);
        Assert.Equal(ha, ContractIntegrityHasher.ComputeContentHash(a));
        Assert.NotEqual(ha, hb);
    }

    [Fact]
    public async Task FullSign_Anchors_AndVerifyMatches()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db, "عقد أرز");
        var service = CreateService(harness.Db);

        var contract = await harness.Db.Contracts
            .Include(c => c.FarmMatch).ThenInclude(m => m.Farm)
            .Include(c => c.FarmMatch).ThenInclude(m => m.SupplyRequest).ThenInclude(r => r.Factory)
            .Include(c => c.FarmMatch).ThenInclude(m => m.SupplyRequest).ThenInclude(r => r.CropType)
            .SingleAsync(c => c.ContractId == seeded.ContractId);

        await service.AnchorIfFullySignedAsync(contract);
        await harness.Db.SaveChangesAsync();

        var active = await service.GetActiveForContractAsync(seeded.ContractId);
        Assert.True(active.IsSuccess);
        Assert.Equal(1, active.Value.ChainIndex);
        Assert.StartsWith("NC-000001-", active.Value.TxRef);

        var verify = await service.VerifyByHashAsync(active.Value.ContentHash);
        Assert.Equal(ContractIntegrityService.OutcomeVerified, verify.Outcome);
        Assert.True(verify.CurrentContentMatches);
        Assert.Equal("Integrity Farm", verify.FarmName);
    }

    [Fact]
    public async Task Regen_Supersedes_OldHashRemainsOnChain()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db, "original body");
        var service = CreateService(harness.Db);

        var contract = await LoadContractAsync(harness.Db, seeded.ContractId);
        await service.AnchorIfFullySignedAsync(contract);
        await harness.Db.SaveChangesAsync();

        var oldHash = (await service.GetActiveForContractAsync(seeded.ContractId)).Value.ContentHash;

        await service.SupersedeActiveAsync(seeded.ContractId);
        contract.GeneratedText = "regenerated body";
        contract.ClearSignatures();
        contract.Status = ContractStatus.PendingSignature;
        await harness.Db.SaveChangesAsync();

        var oldVerify = await service.VerifyByHashAsync(oldHash);
        Assert.Equal(ContractIntegrityService.OutcomeSuperseded, oldVerify.Outcome);

        var active = await service.GetActiveForContractAsync(seeded.ContractId);
        Assert.True(active.IsFailure);
    }

    [Fact]
    public async Task TamperedActiveText_ReportsTampered()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db, "signed body");
        var service = CreateService(harness.Db);

        var contract = await LoadContractAsync(harness.Db, seeded.ContractId);
        await service.AnchorIfFullySignedAsync(contract);
        await harness.Db.SaveChangesAsync();

        var hash = (await service.GetActiveForContractAsync(seeded.ContractId)).Value.ContentHash;

        contract.GeneratedText = "silently edited";
        await harness.Db.SaveChangesAsync();

        var verify = await service.VerifyByHashAsync(hash);
        Assert.Equal(ContractIntegrityService.OutcomeTampered, verify.Outcome);
        Assert.False(verify.CurrentContentMatches);
    }

    [Fact]
    public async Task UnknownHash_ReportsNotFound()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var service = CreateService(harness.Db);
        var verify = await service.VerifyByHashAsync(new string('a', 64));
        Assert.Equal(ContractIntegrityService.OutcomeNotFound, verify.Outcome);
    }

    private static ContractIntegrityService CreateService(NileChainDbContext db) =>
        new(new ContractIntegrityRepository(db));

    private static Task<Contract> LoadContractAsync(NileChainDbContext db, Guid contractId) =>
        db.Contracts
            .Include(c => c.FarmMatch).ThenInclude(m => m.Farm)
            .Include(c => c.FarmMatch).ThenInclude(m => m.SupplyRequest).ThenInclude(r => r.Factory)
            .Include(c => c.FarmMatch).ThenInclude(m => m.SupplyRequest).ThenInclude(r => r.CropType)
            .SingleAsync(c => c.ContractId == contractId);

    private static async Task<(Guid ContractId, Guid FarmUserId, Guid FactoryUserId)> SeedSignedContractAsync(
        NileChainDbContext db,
        string body)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = farmUserId,
                UserName = "farm-i@test.local",
                NormalizedUserName = "FARM-I@TEST.LOCAL",
                Email = "farm-i@test.local",
                NormalizedEmail = "FARM-I@TEST.LOCAL",
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString()
            },
            new ApplicationUser
            {
                Id = factoryUserId,
                UserName = "factory-i@test.local",
                NormalizedUserName = "FACTORY-I@TEST.LOCAL",
                Email = "factory-i@test.local",
                NormalizedEmail = "FACTORY-I@TEST.LOCAL",
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
            Name = "Integrity Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Integrity Factory",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });

        var cropId = Guid.NewGuid();
        db.CropTypes.Add(new CropType { CropTypeId = cropId, Name = "Rice" });

        var requestId = Guid.NewGuid();
        db.SupplyRequests.Add(new SupplyRequest
        {
            RequestId = requestId,
            FactoryId = factoryId,
            CropTypeId = cropId,
            QuantityTons = 12,
            PricePerTon = 9000,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(20),
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
            MatchScore = 88,
            CreatedAt = DateTime.UtcNow
        });

        var contractId = Guid.NewGuid();
        var signedAt = DateTime.UtcNow;
        db.Contracts.Add(new Contract
        {
            ContractId = contractId,
            MatchId = matchId,
            Status = ContractStatus.Signed,
            GeneratedText = body,
            FarmSignedAt = signedAt,
            FactorySignedAt = signedAt,
            SignedAt = signedAt,
            CreatedAt = signedAt,
            RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 1 }
        });

        await db.SaveChangesAsync();
        return (contractId, farmUserId, factoryUserId);
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;

        private SqliteHarness(SqliteConnection keepAlive, NileChainDbContext db)
        {
            _keepAlive = keepAlive;
            Db = db;
        }

        public NileChainDbContext Db { get; }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connectionString = $"Data Source=file:int-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();

            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var db = new SqliteIntegrityDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class SqliteIntegrityDbContext : NileChainDbContext
    {
        public SqliteIntegrityDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
