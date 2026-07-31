using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

/// <summary>
/// Idempotent development-only sample data. Inserts entities only when missing.
/// </summary>
public static class DevelopmentDataSeeder
{
    private const string SeedPassword = "Seed123@!";
    private const string FactoryEmail = "seed.factory@nilechain.dev";
    private static readonly string[] FarmEmails =
    [
        "seed.farm1@nilechain.dev",
        "seed.farm2@nilechain.dev",
        "seed.farm3@nilechain.dev"
    ];

    private static readonly string[] CropNames =
    [
        "Wheat", "Potato", "Corn", "Tomato", "Rice"
    ];

    private static readonly string[] CertificationNames =
    [
        "Organic", "GlobalGAP", "ISO 22000"
    ];

    public static async Task SeedAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        var cropTypes = await SeedCropTypesAsync(db);
        var certifications = await SeedCertificationsAsync(db);

        var factory = await SeedFactoryAsync(db, userManager);
        var farms = await SeedFarmsAsync(db, userManager, cropTypes, certifications);

        var supplyRequest = await SeedSupplyRequestAsync(db, factory, cropTypes);
        await SeedReviewsAsync(db, factory, farms, supplyRequest);

        await db.SaveChangesAsync();
    }

    private static async Task<List<CropType>> SeedCropTypesAsync(NileChainDbContext db)
    {
        var existing = await db.CropTypes.ToListAsync();
        var added = false;

        foreach (var name in CropNames)
        {
            if (existing.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var crop = new CropType
            {
                CropTypeId = Guid.NewGuid(),
                Name = name
            };
            db.CropTypes.Add(crop);
            existing.Add(crop);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return existing;
    }

    private static async Task<List<Certification>> SeedCertificationsAsync(NileChainDbContext db)
    {
        var existing = await db.Certifications.ToListAsync();
        var added = false;

        foreach (var name in CertificationNames)
        {
            if (existing.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var cert = new Certification
            {
                CertificationId = Guid.NewGuid(),
                Name = name
            };
            db.Certifications.Add(cert);
            existing.Add(cert);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return existing;
    }

    private static async Task<Factory> SeedFactoryAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        var existingFactory = await db.Factory
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.User.Email == FactoryEmail);

        if (existingFactory is not null)
            return existingFactory;

        var user = await userManager.FindByEmailAsync(FactoryEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = FactoryEmail,
                Email = FactoryEmail,
                EmailConfirmed = true,
                IsVerified = true,
                PhoneNumber = "01000000001",
                CreatedAt = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(user, SeedPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    "Failed to seed factory user: " +
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));

            await userManager.AddToRoleAsync(user, AppRoles.Factory);
        }

        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Nile Delta Processing Co.",
            Location = "10th of Ramadan",
            Governorate = "Sharqia",
            IndustryType = "Food Processing",
            IsVerified = true,
            AverageRating = 4.2m,
            RatingCount = 3,
            CreatedAt = DateTime.UtcNow
        };

        db.Factory.Add(factory);
        await db.SaveChangesAsync();
        return factory;
    }

    private static async Task<List<Farm>> SeedFarmsAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<CropType> cropTypes,
        List<Certification> certifications)
    {
        var farms = new List<Farm>();

        var farmSpecs = new[]
        {
            new { Email = FarmEmails[0], Name = "Green Valley Farm", Governorate = "Giza", Location = "Abu Sir", Size = 25m, Soil = SoilType.Loamy, Verified = true, Risk = 78m, Crops = new[] { "Wheat", "Corn" }, Certs = new[] { "Organic", "GlobalGAP" } },
            new { Email = FarmEmails[1], Name = "Delta Harvest Farm", Governorate = "Sharqia", Location = "Zagazig", Size = 40m, Soil = SoilType.Clay, Verified = true, Risk = 65m, Crops = new[] { "Wheat", "Potato", "Rice" }, Certs = new[] { "GlobalGAP" } },
            new { Email = FarmEmails[2], Name = "Sunrise Fields", Governorate = "Beheira", Location = "Damanhur", Size = 18m, Soil = SoilType.Sandy, Verified = false, Risk = 52m, Crops = new[] { "Tomato", "Potato" }, Certs = Array.Empty<string>() }
        };

        foreach (var spec in farmSpecs)
        {
            var existingFarm = await db.Farm
                .Include(f => f.User)
                .Include(f => f.CropTypes)
                .Include(f => f.FarmCertifications)
                .FirstOrDefaultAsync(f => f.User.Email == spec.Email);

            if (existingFarm is not null)
            {
                farms.Add(existingFarm);
                continue;
            }

            var user = await userManager.FindByEmailAsync(spec.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = spec.Email,
                    Email = spec.Email,
                    EmailConfirmed = true,
                    IsVerified = spec.Verified,
                    PhoneNumber = "0100000000" + (farms.Count + 2),
                    CreatedAt = DateTime.UtcNow
                };
                var createResult = await userManager.CreateAsync(user, SeedPassword);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to seed farm user {spec.Email}: " +
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));

                await userManager.AddToRoleAsync(user, AppRoles.Farm);
            }

            var farm = new Farm
            {
                FarmId = Guid.NewGuid(),
                UserId = user.Id,
                Name = spec.Name,
                Location = spec.Location,
                Governorate = spec.Governorate,
                SizeInFeddans = spec.Size,
                SoilType = spec.Soil,
                RiskScore = spec.Risk,
                IsVerified = spec.Verified,
                ProfileComplete = true,
                AverageRating = 4.0m,
                RatingCount = 1,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var cropName in spec.Crops)
            {
                var crop = cropTypes.First(c => c.Name.Equals(cropName, StringComparison.OrdinalIgnoreCase));
                farm.CropTypes.Add(crop);
            }

            foreach (var certName in spec.Certs)
            {
                var cert = certifications.First(c => c.Name.Equals(certName, StringComparison.OrdinalIgnoreCase));
                farm.FarmCertifications.Add(new FarmCertification
                {
                    FarmId = farm.FarmId,
                    CertificationId = cert.CertificationId,
                    IssuedAt = DateTime.UtcNow.AddMonths(-6),
                    ExpiresAt = DateTime.UtcNow.AddYears(1)
                });
            }

            db.Farm.Add(farm);
            farms.Add(farm);
        }

        await db.SaveChangesAsync();
        return farms;
    }

    private static async Task<SupplyRequest> SeedSupplyRequestAsync(
        NileChainDbContext db,
        Factory factory,
        List<CropType> cropTypes)
    {
        var wheat = cropTypes.First(c => c.Name.Equals("Wheat", StringComparison.OrdinalIgnoreCase));

        var existing = await db.SupplyRequests
            .FirstOrDefaultAsync(r =>
                r.FactoryId == factory.FactoryId
                && r.CropTypeId == wheat.CropTypeId
                && r.Status == SupplyRequestStatus.Pending);

        if (existing is not null)
            return existing;

        var request = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = wheat.CropTypeId,
            QuantityTons = 50,
            QualitySpecs = "Grade A, moisture < 12%",
            PricePerTon = 12000,
            DeliveryDate = DateTime.UtcNow.AddDays(45),
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.SupplyRequests.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    private static async Task SeedReviewsAsync(
        NileChainDbContext db,
        Factory factory,
        List<Farm> farms,
        SupplyRequest supplyRequest)
    {
        // Reviews require Contract → FarmMatch. Seed supporting graph only when reviews are missing.
        var targetFarm = farms.FirstOrDefault(f => f.IsVerified) ?? farms.First();

        var hasReview = await db.Reviews.AnyAsync(r => r.TargetId == targetFarm.UserId);
        if (hasReview)
            return;

        var match = await db.FarmMatches
            .Include(m => m.Contract)
            .FirstOrDefaultAsync(m =>
                m.RequestId == supplyRequest.RequestId
                && m.FarmId == targetFarm.FarmId);

        if (match is null)
        {
            match = new FarmMatch
            {
                MatchId = Guid.NewGuid(),
                RequestId = supplyRequest.RequestId,
                FarmId = targetFarm.FarmId,
                MatchScore = 85,
                RiskScore = targetFarm.RiskScore ?? 70,
                Status = FarmMatchStatus.Accepted,
                CreatedAt = DateTime.UtcNow
            };
            db.FarmMatches.Add(match);
            await db.SaveChangesAsync();
        }

        var contract = match.Contract
            ?? await db.Contracts.FirstOrDefaultAsync(c => c.MatchId == match.MatchId);

        if (contract is null)
        {
            contract = new Contract
            {
                ContractId = Guid.NewGuid(),
                MatchId = match.MatchId,
                GeneratedText = "Seed development contract for Wheat supply.",
                Status = ContractStatus.Signed,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                SignedAt = DateTime.UtcNow.AddDays(-7)
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
        }

        if (!await db.Reviews.AnyAsync(r => r.ContractId == contract.ContractId))
        {
            db.Reviews.Add(new Review
            {
                ReviewId = Guid.NewGuid(),
                ContractId = contract.ContractId,
                ReviewerId = factory.UserId,
                TargetId = targetFarm.UserId,
                Rating = 5,
                Comment = "Reliable delivery and good wheat quality.",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            });

            // Second review from another signed path is optional; add one more for a second farm if available
            var secondFarm = farms.FirstOrDefault(f => f.FarmId != targetFarm.FarmId);
            if (secondFarm is not null
                && !await db.Reviews.AnyAsync(r => r.TargetId == secondFarm.UserId))
            {
                var match2 = new FarmMatch
                {
                    MatchId = Guid.NewGuid(),
                    RequestId = supplyRequest.RequestId,
                    FarmId = secondFarm.FarmId,
                    MatchScore = 72,
                    RiskScore = secondFarm.RiskScore ?? 60,
                    Status = FarmMatchStatus.Accepted,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                };
                db.FarmMatches.Add(match2);
                await db.SaveChangesAsync();

                var contract2 = new Contract
                {
                    ContractId = Guid.NewGuid(),
                    MatchId = match2.MatchId,
                    GeneratedText = "Seed development contract #2.",
                    Status = ContractStatus.Signed,
                    CreatedAt = DateTime.UtcNow.AddDays(-18),
                    SignedAt = DateTime.UtcNow.AddDays(-15)
                };
                db.Contracts.Add(contract2);
                await db.SaveChangesAsync();

                db.Reviews.Add(new Review
                {
                    ReviewId = Guid.NewGuid(),
                    ContractId = contract2.ContractId,
                    ReviewerId = factory.UserId,
                    TargetId = secondFarm.UserId,
                    Rating = 4,
                    Comment = "Good quality, slight delay on delivery.",
                    CreatedAt = DateTime.UtcNow.AddDays(-12)
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
