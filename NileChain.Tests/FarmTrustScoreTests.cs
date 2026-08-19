using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Dtos.Review;
using NileChain.Application.Services;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class FarmTrustScoreTests
{
    [Fact]
    public void EmptyFarm_ScoresZero_AndIsLowTrust()
    {
        var result = FarmTrustScore.Compute(new FarmTrustInputs());

        Assert.Equal(0m, result.Overall);
        Assert.Equal(FarmTrustBand.Low, result.Band);
    }

    [Fact]
    public void CompleteProfile_CapsAtProfileMax()
    {
        var result = FarmTrustScore.Compute(new FarmTrustInputs(
            HasName: true,
            HasLocation: true,
            HasGovernorate: true,
            HasPhone: true,
            HasSize: true,
            HasDocuments: true,
            HasDescription: true,
            HasImages: true,
            HasCrops: true,
            ValidCertificationCount: 0,
            SignedContractCount: 0,
            AverageRating: 0m));

        Assert.Equal(FarmTrustScore.ProfileMaxPoints, result.Profile);
        Assert.Equal(FarmTrustScore.ProfileMaxPoints, result.Overall);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 12.5)]
    [InlineData(2, 25)]
    [InlineData(5, 25)]
    public void Certifications_AreWorth12Point5Each_CappedAt25(int count, decimal expected)
    {
        var result = FarmTrustScore.Compute(
            new FarmTrustInputs { ValidCertificationCount = count });

        Assert.Equal(expected, result.Certifications);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 20)]
    [InlineData(3, 30)]
    [InlineData(9, 30)]
    public void SignedContracts_AreWorth10Each_CappedAt30(int count, decimal expected)
    {
        var result = FarmTrustScore.Compute(
            new FarmTrustInputs { SignedContractCount = count });

        Assert.Equal(expected, result.ContractHistory);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.5, 10)]
    [InlineData(4, 16)]
    [InlineData(5, 20)]
    public void Rating_ScalesFiveStarsTo20Points(decimal average, decimal expected)
    {
        var result = FarmTrustScore.Compute(
            new FarmTrustInputs { AverageRating = average });

        Assert.Equal(expected, result.Rating);
    }

    [Fact]
    public void OutOfRangeRating_IsClamped_NotAmplified()
    {
        var above = FarmTrustScore.Compute(new FarmTrustInputs { AverageRating = 9m });
        var below = FarmTrustScore.Compute(new FarmTrustInputs { AverageRating = -3m });

        Assert.Equal(FarmTrustScore.RatingMaxPoints, above.Rating);
        Assert.Equal(0m, below.Rating);
    }

    [Fact]
    public void Overall_NeverExceeds100()
    {
        var result = FarmTrustScore.Compute(new FarmTrustInputs(
            HasName: true,
            HasLocation: true,
            HasGovernorate: true,
            HasPhone: true,
            HasSize: true,
            HasDocuments: true,
            HasDescription: true,
            HasImages: true,
            HasCrops: true,
            ValidCertificationCount: 10,
            SignedContractCount: 10,
            AverageRating: 5m));

        Assert.Equal(FarmTrustScore.MaxOverallScore, result.Overall);
        Assert.Equal(FarmTrustBand.High, result.Band);
    }

    [Theory]
    [InlineData(0, FarmTrustBand.Low)]
    [InlineData(39.9, FarmTrustBand.Low)]
    [InlineData(40, FarmTrustBand.Medium)]
    [InlineData(69.9, FarmTrustBand.Medium)]
    [InlineData(70, FarmTrustBand.High)]
    [InlineData(100, FarmTrustBand.High)]
    public void Bands_UseInclusiveLowerBounds(decimal score, FarmTrustBand expected)
    {
        Assert.Equal(expected, FarmTrustScore.ToBand(score));
    }

    [Fact]
    public void HighTrust_ReadsAsLowRisk_InArabicLabel()
    {
        // The score is trust, the label is risk — the inversion is intentional.
        Assert.Equal(FarmTrustLevelText.LowRisk, FarmTrustLevelText.Arabic(90m));
        Assert.Equal(FarmTrustLevelText.MediumRisk, FarmTrustLevelText.Arabic(55m));
        Assert.Equal(FarmTrustLevelText.HighRisk, FarmTrustLevelText.Arabic(10m));
    }

    [Fact]
    public void UnscoredFarm_ReadsAsMediumRisk_NotHighRisk()
    {
        Assert.Equal(FarmTrustLevelText.MediumRisk, FarmTrustLevelText.Arabic((decimal?)null));
    }

    [Fact]
    public void InputsFrom_IgnoresCertificationsNotGrantedByAdmin()
    {
        var now = DateTime.UtcNow;
        var farm = new Farm
        {
            FarmId = Guid.NewGuid(),
            Name = "Trust Farm",
            FarmCertifications = new List<FarmCertification>
            {
                new() { GrantedByAdminUserId = null, ExpiresAt = now.AddYears(1) },
                new() { GrantedByAdminUserId = Guid.NewGuid(), ExpiresAt = now.AddDays(-1) },
                new() { GrantedByAdminUserId = Guid.NewGuid(), ExpiresAt = now.AddYears(1) }
            }
        };

        var inputs = FarmTrustScore.InputsFrom(farm, signedContractCount: 0, averageRating: 0m, now);

        Assert.Equal(1, inputs.ValidCertificationCount);
    }

    [Fact]
    public async Task FirstReview_UpdatesAverageImmediately_NotOneReviewLate()
    {
        await using var harness = await TrustHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateReviewService(harness.Db);

        var created = await service.CreateReviewAsync(
            seeded.FactoryUserId,
            new CreateReviewRequest
            {
                ContractId = seeded.ContractId,
                TargetId = seeded.FarmUserId,
                Rating = 4
            });

        Assert.True(created.IsSuccess);

        var farm = await harness.Db.Farm.AsNoTracking()
            .FirstAsync(f => f.UserId == seeded.FarmUserId);
        Assert.Equal(4m, farm.AverageRating);
        Assert.Equal(1, farm.RatingCount);
    }

    [Fact]
    public async Task Review_RefreshesCachedTrustScore_UsedForRanking()
    {
        await using var harness = await TrustHarness.CreateAsync();
        var seeded = await SeedSignedContractAsync(harness.Db);
        var service = CreateReviewService(harness.Db);

        var before = await harness.Db.Farm.AsNoTracking()
            .FirstAsync(f => f.UserId == seeded.FarmUserId);
        Assert.Null(before.RiskScore);

        var created = await service.CreateReviewAsync(
            seeded.FactoryUserId,
            new CreateReviewRequest
            {
                ContractId = seeded.ContractId,
                TargetId = seeded.FarmUserId,
                Rating = 5
            });
        Assert.True(created.IsSuccess);

        var after = await harness.Db.Farm.AsNoTracking()
            .FirstAsync(f => f.UserId == seeded.FarmUserId);

        // Profile (name + governorate = 8) + one signed contract (10) + 5 stars (20).
        Assert.Equal(38m, after.RiskScore);
    }

    private static ReviewService CreateReviewService(NileChainDbContext db) => new(
        new Repository<Review>(db),
        new Repository<Contract>(db),
        new Repository<FarmMatch>(db),
        new FarmRepository(db),
        new Repository<Factory>(db),
        new Repository<SupplyRequest>(db),
        new UnitOfWork(db));

    private static async Task<(
        Guid ContractId,
        Guid FarmUserId,
        Guid FactoryUserId)> SeedSignedContractAsync(NileChainDbContext db)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();
        db.Users.AddRange(
            User(farmUserId, "farm-trust@test.local"),
            User(factoryUserId, "factory-trust@test.local"));

        var farmId = Guid.NewGuid();
        var factoryId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Trust Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Trust Factory",
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

    private sealed class TrustHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;

        private TrustHarness(SqliteConnection keepAlive, NileChainDbContext db)
        {
            _keepAlive = keepAlive;
            Db = db;
        }

        public NileChainDbContext Db { get; }

        public static async Task<TrustHarness> CreateAsync()
        {
            var cs = $"Data Source=file:trust-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(cs);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(cs)
                .Options;
            var db = new TrustDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TrustHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class TrustDbContext : NileChainDbContext
    {
        public TrustDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

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
