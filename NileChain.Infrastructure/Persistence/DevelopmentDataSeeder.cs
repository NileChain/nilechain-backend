using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NileChain.Infrastructure.Persistence;

/// <summary>
/// Idempotent development-only sample data. Inserts entities only when missing.
/// Does not alter production schema; uses existing domain fields only.
/// </summary>
public static class DevelopmentDataSeeder
{
    private const string SeedPassword = "Seed123@!";
    private const string SeedMarker = "[SEED]";
    private const string MarketPriceSource = "DevelopmentSeed";

    private static readonly string[] FactoryEmails =
    [
        "seed.factory1@nilechain.dev",
        "seed.factory2@nilechain.dev"
    ];

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

    /// <summary>
    /// Governorate reference values (no Governorate entity exists — used as string lookup + MarketPrice rows).
    /// </summary>
    private static readonly string[] Governorates =
    [
        "Cairo", "Giza", "Sharqia", "Beheira", "Dakahlia", "Alexandria", "Minya", "Assiut"
    ];

    public static async Task<DevelopmentSeedReport> SeedAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger? logger = null)
    {
        var cropTypes = await SeedCropTypesAsync(db);
        var certifications = await SeedCertificationsAsync(db);
        await SeedGovernorateMarketPricesAsync(db, cropTypes);

        var factories = await SeedFactoriesAsync(db, userManager);
        factories = await db.Factory
            .Include(f => f.User)
            .Where(f => f.User.Email != null && FactoryEmails.Contains(f.User.Email))
            .OrderBy(f => f.User.Email)
            .ToListAsync();

        var farms = await SeedFarmsAsync(db, userManager, cropTypes, certifications);
        farms = await db.Farm
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Where(f => f.User.Email != null && FarmEmails.Contains(f.User.Email))
            .OrderBy(f => f.User.Email)
            .ToListAsync();

        await SeedFarmDocumentsAsync(db, farms);

        var supplyRequests = await SeedSupplyRequestsAsync(db, factories, cropTypes);
        var matches = await SeedFarmMatchesAsync(db, farms, supplyRequests);
        var contracts = await SeedContractsAsync(db, matches);
        await SeedMessagesAsync(db, matches, farms, factories);
        await SeedReviewsAsync(db, contracts, matches, farms, factories);
        await RecalculateRatingsAsync(db, farms, factories);
        await SeedNotificationsAsync(db, userManager, farms, factories);

        await db.SaveChangesAsync();

        var report = await BuildReportAsync(db, userManager, supplyRequests);
        logger?.LogInformation("{Report}", report.ToSummary());
        Console.WriteLine(report.ToSummary());
        return report;
    }

    // -------------------------------------------------------------------------
    // Reference data
    // -------------------------------------------------------------------------

    private static async Task<List<CropType>> SeedCropTypesAsync(NileChainDbContext db)
    {
        var existing = await db.CropTypes.ToListAsync();
        var added = false;

        foreach (var name in CropNames)
        {
            if (existing.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = name };
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

            var cert = new Certification { CertificationId = Guid.NewGuid(), Name = name };
            db.Certifications.Add(cert);
            existing.Add(cert);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return existing;
    }

    private static async Task SeedGovernorateMarketPricesAsync(
        NileChainDbContext db,
        List<CropType> cropTypes)
    {
        var existing = await db.MarketPrices
            .Where(p => p.Source == MarketPriceSource)
            .ToListAsync();

        var basePrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wheat"] = 12000m,
            ["Potato"] = 8000m,
            ["Corn"] = 9500m,
            ["Tomato"] = 7000m,
            ["Rice"] = 11000m
        };

        var added = false;
        var offset = 0;
        foreach (var gov in Governorates)
        {
            foreach (var crop in cropTypes.Where(c => CropNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase)))
            {
                if (existing.Any(p =>
                        p.CropTypeId == crop.CropTypeId
                        && string.Equals(p.Governorate, gov, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var price = basePrices.GetValueOrDefault(crop.Name, 9000m) + (offset % 5) * 150m;
                db.MarketPrices.Add(new MarketPrice
                {
                    PriceId = Guid.NewGuid(),
                    CropTypeId = crop.CropTypeId,
                    Governorate = gov,
                    PricePerTon = price,
                    Source = MarketPriceSource,
                    RecordedAt = DateTime.UtcNow.AddDays(-offset)
                });
                added = true;
                offset++;
            }
        }

        if (added)
            await db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // Users / profiles
    // -------------------------------------------------------------------------

    private static async Task<List<Factory>> SeedFactoriesAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        var specs = new[]
        {
            new
            {
                Email = FactoryEmails[0],
                Name = "Nile Delta Processing Co.",
                Governorate = "Sharqia",
                Location = "10th of Ramadan",
                // Domain has no Capacity/PreferredCrops — IndustryType carries capacity note for dev UX.
                IndustryType = "Food Processing | Capacity ~500 tons/month | Preferred: Wheat, Corn, Rice",
                Phone = "01010000001",
                Verified = true
            },
            new
            {
                Email = FactoryEmails[1],
                Name = "Cairo AgriFood Industries",
                Governorate = "Cairo",
                Location = "6th of October",
                IndustryType = "Canning & Packaging | Capacity ~320 tons/month | Preferred: Tomato, Potato",
                Phone = "01010000002",
                Verified = true
            }
        };

        var factories = new List<Factory>();

        foreach (var spec in specs)
        {
            var existing = await db.Factory
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.User.Email == spec.Email);

            if (existing is not null)
            {
                factories.Add(existing);
                continue;
            }

            var user = await EnsureUserAsync(
                userManager,
                spec.Email,
                AppRoles.Factory,
                spec.Phone,
                isVerified: spec.Verified);

            var factory = new Factory
            {
                FactoryId = Guid.NewGuid(),
                UserId = user.Id,
                Name = spec.Name,
                Location = spec.Location,
                Governorate = spec.Governorate,
                IndustryType = spec.IndustryType,
                IsVerified = spec.Verified,
                AverageRating = 0m,
                RatingCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };

            db.Factory.Add(factory);
            factories.Add(factory);
        }

        await db.SaveChangesAsync();
        return factories;
    }

    private static async Task<List<Farm>> SeedFarmsAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<CropType> cropTypes,
        List<Certification> certifications)
    {
        var farmSpecs = new[]
        {
            new
            {
                Email = FarmEmails[0],
                Name = "Green Valley Farm",
                Governorate = "Giza",
                Location = "Abu Sir",
                Size = 25m,
                Soil = SoilType.Loamy,
                Verified = true,
                Risk = 78m,
                Phone = "01020000001",
                Crops = new[] { "Wheat", "Corn" },
                Certs = new[] { "Organic", "GlobalGAP" }
            },
            new
            {
                Email = FarmEmails[1],
                Name = "Delta Harvest Farm",
                Governorate = "Sharqia",
                Location = "Zagazig",
                Size = 40m,
                Soil = SoilType.Clay,
                Verified = true,
                Risk = 65m,
                Phone = "01020000002",
                Crops = new[] { "Wheat", "Potato", "Rice" },
                Certs = new[] { "GlobalGAP" }
            },
            new
            {
                Email = FarmEmails[2],
                Name = "Sunrise Fields",
                Governorate = "Beheira",
                Location = "Damanhur",
                Size = 18m,
                Soil = SoilType.Sandy,
                Verified = false,
                Risk = 52m,
                Phone = "01020000003",
                Crops = new[] { "Tomato", "Potato" },
                Certs = Array.Empty<string>()
            }
        };

        var farms = new List<Farm>();

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

            var user = await EnsureUserAsync(
                userManager,
                spec.Email,
                AppRoles.Farm,
                spec.Phone,
                isVerified: spec.Verified);

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
                AverageRating = 0m,
                RatingCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-90)
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

    private static async Task SeedFarmDocumentsAsync(NileChainDbContext db, List<Farm> farms)
    {
        var added = false;

        foreach (var farm in farms)
        {
            var fileName = $"{SeedMarker} land-title-{farm.Name.Replace(' ', '-').ToLowerInvariant()}.pdf";
            var exists = await db.FarmDocuments.AnyAsync(d =>
                d.FarmId == farm.FarmId && d.FileName == fileName);
            if (exists)
                continue;

            db.FarmDocuments.Add(new FarmDocument
            {
                FarmDocumentId = Guid.NewGuid(),
                FarmId = farm.FarmId,
                FileName = fileName,
                FileUrl = $"https://res.cloudinary.com/demo/raw/upload/seed/{farm.FarmId}/land-title.pdf",
                FileSize = 245_760,
                FileType = "application/pdf",
                PublicId = $"seed/{farm.FarmId}/land-title",
                UploadedAt = DateTime.UtcNow.AddDays(-30)
            });

            if (farm.IsVerified)
            {
                var certDoc = $"{SeedMarker} organic-cert-{farm.FarmId:N}.pdf";
                if (!await db.FarmDocuments.AnyAsync(d => d.FarmId == farm.FarmId && d.FileName == certDoc))
                {
                    db.FarmDocuments.Add(new FarmDocument
                    {
                        FarmDocumentId = Guid.NewGuid(),
                        FarmId = farm.FarmId,
                        FileName = certDoc,
                        FileUrl = $"https://res.cloudinary.com/demo/raw/upload/seed/{farm.FarmId}/organic-cert.pdf",
                        FileSize = 128_000,
                        FileType = "application/pdf",
                        PublicId = $"seed/{farm.FarmId}/organic-cert",
                        UploadedAt = DateTime.UtcNow.AddDays(-20)
                    });
                }
            }

            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role,
        string phone,
        bool isVerified)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsVerified = isVerified,
            IsActive = true,
            PhoneNumber = phone,
            CreatedAt = DateTime.UtcNow.AddDays(-100)
        };

        var createResult = await userManager.CreateAsync(user, SeedPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(
                $"Failed to seed user {email}: " +
                string.Join("; ", createResult.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    // -------------------------------------------------------------------------
    // Supply requests
    // -------------------------------------------------------------------------

    private sealed record SupplyRequestSpec(
        string Key,
        int FactoryIndex,
        string Crop,
        decimal QuantityTons,
        string GovernorateHint,
        decimal? PricePerTon,
        SupplyRequestStatus Status,
        int DeliveryDays,
        string Specs);

    private static async Task<List<SupplyRequest>> SeedSupplyRequestsAsync(
        NileChainDbContext db,
        List<Factory> factories,
        List<CropType> cropTypes)
    {
        // Prefer new factory emails; fall back by list order.
        var factory1 = factories.FirstOrDefault(f => f.User?.Email == FactoryEmails[0])
                       ?? factories.ElementAtOrDefault(0)
                       ?? throw new InvalidOperationException("No factory available for seed.");
        var factory2 = factories.FirstOrDefault(f => f.User?.Email == FactoryEmails[1])
                       ?? factories.ElementAtOrDefault(1)
                       ?? factory1;

        // Reload with users if missing
        if (factory1.User is null)
            factory1 = await db.Factory.Include(f => f.User).FirstAsync(f => f.FactoryId == factory1.FactoryId);
        if (factory2.User is null)
            factory2 = await db.Factory.Include(f => f.User).FirstAsync(f => f.FactoryId == factory2.FactoryId);

        var orderedFactories = new[] { factory1, factory2 };

        var specs = new List<SupplyRequestSpec>
        {
            new("WHEAT-PENDING", 0, "Wheat", 50m, "Sharqia", 12000m, SupplyRequestStatus.Pending, 45,
                "Grade A, moisture < 12%"),
            new("CORN-MATCHED", 0, "Corn", 80m, "Giza", 9500m, SupplyRequestStatus.Matched, 30,
                "Yellow corn, low aflatoxin"),
            new("RICE-FULFILLED", 0, "Rice", 40m, "Dakahlia", 11000m, SupplyRequestStatus.Fulfilled, -20,
                "Egyptian short-grain"),
            new("TOMATO-PENDING", 1, "Tomato", 25m, "Beheira", 7000m, SupplyRequestStatus.Pending, 20,
                "Processing grade, firm"),
            new("POTATO-CANCELLED", 1, "Potato", 60m, "Minya", 8000m, SupplyRequestStatus.Cancelled, 60,
                "Industrial size 45-65mm"),
            new("WHEAT-MATCHED-F2", 1, "Wheat", 35m, "Cairo", 11800m, SupplyRequestStatus.Matched, 40,
                "Bread wheat, protein > 11%")
        };

        var results = new List<SupplyRequest>();

        foreach (var spec in specs)
        {
            var crop = cropTypes.First(c => c.Name.Equals(spec.Crop, StringComparison.OrdinalIgnoreCase));
            var factory = orderedFactories[spec.FactoryIndex];
            var marker = $"{SeedMarker}:{spec.Key}";

            var existing = await db.SupplyRequests
                .FirstOrDefaultAsync(r => r.QualitySpecs != null && r.QualitySpecs.Contains(marker));

            if (existing is not null)
            {
                results.Add(existing);
                continue;
            }

            var request = new SupplyRequest
            {
                RequestId = Guid.NewGuid(),
                FactoryId = factory.FactoryId,
                CropTypeId = crop.CropTypeId,
                QuantityTons = spec.QuantityTons,
                QualitySpecs = $"{marker} | Gov:{spec.GovernorateHint} | {spec.Specs}",
                PricePerTon = spec.PricePerTon,
                DeliveryDate = DateTime.UtcNow.AddDays(spec.DeliveryDays),
                Status = spec.Status,
                CreatedAt = DateTime.UtcNow.AddDays(-Math.Abs(spec.DeliveryDays) - 5)
            };

            db.SupplyRequests.Add(request);
            results.Add(request);
        }

        await db.SaveChangesAsync();
        return results;
    }

    // -------------------------------------------------------------------------
    // Matches / contracts / messages / reviews
    // -------------------------------------------------------------------------

    private sealed record MatchSpec(
        string RequestKey,
        int FarmIndex,
        decimal MatchScore,
        decimal RiskScore,
        FarmMatchStatus Status);

    private static async Task<List<FarmMatch>> SeedFarmMatchesAsync(
        NileChainDbContext db,
        List<Farm> farms,
        List<SupplyRequest> requests)
    {
        SupplyRequest Req(string key) =>
            requests.First(r => r.QualitySpecs != null && r.QualitySpecs.Contains($"{SeedMarker}:{key}"));

        var specs = new List<MatchSpec>
        {
            // Wheat pending — multiple candidates, mixed statuses
            new("WHEAT-PENDING", 0, 92m, 78m, FarmMatchStatus.Proposed),
            new("WHEAT-PENDING", 1, 85m, 65m, FarmMatchStatus.Accepted),
            new("WHEAT-PENDING", 2, 61m, 52m, FarmMatchStatus.Rejected),

            // Corn matched
            new("CORN-MATCHED", 0, 88m, 78m, FarmMatchStatus.Accepted),
            new("CORN-MATCHED", 1, 74m, 65m, FarmMatchStatus.Proposed),
            new("CORN-MATCHED", 2, 55m, 52m, FarmMatchStatus.Expired),

            // Rice fulfilled
            new("RICE-FULFILLED", 1, 90m, 65m, FarmMatchStatus.Accepted),
            new("RICE-FULFILLED", 0, 70m, 78m, FarmMatchStatus.Rejected),

            // Tomato pending
            new("TOMATO-PENDING", 2, 86m, 52m, FarmMatchStatus.Proposed),
            new("TOMATO-PENDING", 1, 79m, 65m, FarmMatchStatus.Proposed),
            new("TOMATO-PENDING", 0, 58m, 78m, FarmMatchStatus.Rejected),

            // Potato cancelled — historical matches
            new("POTATO-CANCELLED", 2, 80m, 52m, FarmMatchStatus.Expired),
            new("POTATO-CANCELLED", 1, 66m, 65m, FarmMatchStatus.Rejected),

            // Wheat matched factory 2
            new("WHEAT-MATCHED-F2", 0, 91m, 78m, FarmMatchStatus.Accepted),
            new("WHEAT-MATCHED-F2", 1, 83m, 65m, FarmMatchStatus.Proposed)
        };

        var matches = new List<FarmMatch>();
        var added = false;

        foreach (var spec in specs)
        {
            var request = Req(spec.RequestKey);
            var farm = farms[spec.FarmIndex];

            var existing = await db.FarmMatches
                .FirstOrDefaultAsync(m => m.RequestId == request.RequestId && m.FarmId == farm.FarmId);

            if (existing is not null)
            {
                matches.Add(existing);
                continue;
            }

            var match = new FarmMatch
            {
                MatchId = Guid.NewGuid(),
                RequestId = request.RequestId,
                FarmId = farm.FarmId,
                MatchScore = spec.MatchScore,
                RiskScore = spec.RiskScore,
                Status = spec.Status,
                CreatedAt = DateTime.UtcNow.AddDays(-10 - (int)(spec.MatchScore % 7))
            };

            db.FarmMatches.Add(match);
            matches.Add(match);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return matches;
    }

    private static async Task<List<Contract>> SeedContractsAsync(
        NileChainDbContext db,
        List<FarmMatch> matches)
    {
        // Domain ContractStatus: Draft, PendingSignature, Signed, Cancelled (no Completed).
        var accepted = matches.Where(m => m.Status == FarmMatchStatus.Accepted).ToList();
        if (accepted.Count == 0)
            return await db.Contracts.ToListAsync();

        var statusCycle = new[]
        {
            ContractStatus.Signed,
            ContractStatus.Draft,
            ContractStatus.PendingSignature,
            ContractStatus.Signed,
            ContractStatus.Cancelled
        };

        var contracts = new List<Contract>();
        var added = false;

        for (var i = 0; i < accepted.Count; i++)
        {
            var match = accepted[i];
            var existing = await db.Contracts.FirstOrDefaultAsync(c => c.MatchId == match.MatchId);
            if (existing is not null)
            {
                contracts.Add(existing);
                continue;
            }

            var status = statusCycle[i % statusCycle.Length];
            var contract = new Contract
            {
                ContractId = Guid.NewGuid(),
                MatchId = match.MatchId,
                GeneratedText = $"{SeedMarker} Development contract for match {match.MatchId:N}. Status={status}.",
                PdfUrl = status == ContractStatus.Signed
                    ? $"https://res.cloudinary.com/demo/raw/upload/seed/contracts/{match.MatchId:N}.pdf"
                    : null,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-15 + i),
                SignedAt = status == ContractStatus.Signed ? DateTime.UtcNow.AddDays(-10 + i) : null
            };

            db.Contracts.Add(contract);
            contracts.Add(contract);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return contracts;
    }

    private static async Task SeedMessagesAsync(
        NileChainDbContext db,
        List<FarmMatch> matches,
        List<Farm> farms,
        List<Factory> factories)
    {
        // Conversations on accepted + some proposed matches
        var conversational = matches
            .Where(m => m.Status is FarmMatchStatus.Accepted or FarmMatchStatus.Proposed)
            .Take(6)
            .ToList();

        var farmById = farms.ToDictionary(f => f.FarmId);
        var requestFactoryIds = await db.SupplyRequests
            .Where(r => conversational.Select(m => m.RequestId).Contains(r.RequestId))
            .ToDictionaryAsync(r => r.RequestId, r => r.FactoryId);

        var factoryById = factories.ToDictionary(f => f.FactoryId);
        var added = false;

        foreach (var match in conversational)
        {
            if (!farmById.TryGetValue(match.FarmId, out var farm))
                continue;
            if (!requestFactoryIds.TryGetValue(match.RequestId, out var factoryId))
                continue;
            if (!factoryById.TryGetValue(factoryId, out var factory))
                continue;

            var factoryUserId = factory.UserId;
            var farmUserId = farm.UserId;
            var threads = new (Guid From, Guid To, bool Read, string Content)[]
            {
                (factoryUserId, farmUserId, true, $"{SeedMarker} Hello - interested in your crop for our supply request."),
                (farmUserId, factoryUserId, true, $"{SeedMarker} Thanks - we can deliver within the requested window."),
                (factoryUserId, farmUserId, false, $"{SeedMarker} Great. Please confirm quality specs and pricing."),
                (farmUserId, factoryUserId, false, $"{SeedMarker} Specs confirmed. Waiting on your contract draft.")
            };

            var day = 0;
            foreach (var msg in threads)
            {
                var exists = await db.Messages.AnyAsync(m =>
                    m.MatchId == match.MatchId && m.Content == msg.Content);
                if (exists)
                    continue;

                db.Messages.Add(new Message
                {
                    MessageId = Guid.NewGuid(),
                    MatchId = match.MatchId,
                    SenderId = msg.From,
                    ReceiverId = msg.To,
                    Content = msg.Content,
                    IsRead = msg.Read,
                    CreatedAt = DateTime.UtcNow.AddDays(-5 + day).AddHours(day)
                });
                day++;
                added = true;
            }
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task SeedReviewsAsync(
        NileChainDbContext db,
        List<Contract> contracts,
        List<FarmMatch> matches,
        List<Farm> farms,
        List<Factory> factories)
    {
        var signed = contracts.Where(c => c.Status == ContractStatus.Signed).ToList();
        if (signed.Count == 0)
            return;

        var matchById = matches.ToDictionary(m => m.MatchId);
        var farmById = farms.ToDictionary(f => f.FarmId);
        var requestFactory = await db.SupplyRequests.ToDictionaryAsync(r => r.RequestId, r => r.FactoryId);
        var factoryById = factories.ToDictionary(f => f.FactoryId);

        var ratings = new[] { 5, 4, 5, 3, 4 };
        var comments = new[]
        {
            "Reliable delivery and excellent quality.",
            "Good quality, slight delay on delivery.",
            "Consistent supply — will work again.",
            "Acceptable, packaging needs improvement.",
            "Professional communication throughout."
        };

        var added = false;
        for (var i = 0; i < signed.Count; i++)
        {
            var contract = signed[i];
            if (await db.Reviews.AnyAsync(r => r.ContractId == contract.ContractId))
                continue;

            if (!matchById.TryGetValue(contract.MatchId, out var match))
                continue;
            if (!farmById.TryGetValue(match.FarmId, out var farm))
                continue;
            if (!requestFactory.TryGetValue(match.RequestId, out var factoryId))
                continue;
            if (!factoryById.TryGetValue(factoryId, out var factory))
                continue;

            // Factory → Farm
            db.Reviews.Add(new Review
            {
                ReviewId = Guid.NewGuid(),
                ContractId = contract.ContractId,
                ReviewerId = factory.UserId,
                TargetId = farm.UserId,
                Rating = ratings[i % ratings.Length],
                Comment = $"{SeedMarker} {comments[i % comments.Length]}",
                CreatedAt = DateTime.UtcNow.AddDays(-8 + i)
            });

            // Farm → Factory (reciprocal)
            db.Reviews.Add(new Review
            {
                ReviewId = Guid.NewGuid(),
                ContractId = contract.ContractId,
                ReviewerId = farm.UserId,
                TargetId = factory.UserId,
                Rating = ratings[(i + 2) % ratings.Length],
                Comment = $"{SeedMarker} Fair terms and timely payment.",
                CreatedAt = DateTime.UtcNow.AddDays(-7 + i)
            });

            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task RecalculateRatingsAsync(
        NileChainDbContext db,
        List<Farm> farms,
        List<Factory> factories)
    {
        var reviews = await db.Reviews.ToListAsync();

        foreach (var farm in farms)
        {
            var farmReviews = reviews.Where(r => r.TargetId == farm.UserId).ToList();
            if (farmReviews.Count == 0)
                continue;

            farm.RatingCount = farmReviews.Count;
            farm.AverageRating = Math.Round((decimal)farmReviews.Average(r => r.Rating), 2);
            db.Farm.Update(farm);
        }

        foreach (var factory in factories)
        {
            var factoryReviews = reviews.Where(r => r.TargetId == factory.UserId).ToList();
            if (factoryReviews.Count == 0)
                continue;

            factory.RatingCount = factoryReviews.Count;
            factory.AverageRating = Math.Round((decimal)factoryReviews.Average(r => r.Rating), 2);
            db.Factory.Update(factory);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedNotificationsAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<Farm> farms,
        List<Factory> factories)
    {
        var admin = await userManager.FindByEmailAsync("admin@gmail.com");
        var items = new List<(Guid UserId, string Title, string Message, string Type, bool IsRead)>();

        foreach (var farm in farms)
        {
            items.Add((farm.UserId, $"{SeedMarker} New match proposed", "A factory proposed a match for your farm.", "Match", false));
            items.Add((farm.UserId, $"{SeedMarker} Profile tip", "Upload remaining documents to improve match score.", "System", true));
        }

        foreach (var factory in factories)
        {
            items.Add((factory.UserId, $"{SeedMarker} Matches ready", "Candidate farms are available for your supply request.", "Match", false));
            items.Add((factory.UserId, $"{SeedMarker} Contract signed", "A farm has signed a supply contract.", "Contract", true));
        }

        if (admin is not null)
        {
            items.Add((admin.Id, $"{SeedMarker} Farm pending verification", "Sunrise Fields awaits admin verification.", "Admin", false));
            items.Add((admin.Id, $"{SeedMarker} Weekly digest", "Development seed loaded successfully.", "Admin", true));
        }

        var added = false;
        foreach (var item in items)
        {
            var exists = await db.Notifications.AnyAsync(n =>
                n.UserId == item.UserId && n.Title == item.Title);
            if (exists)
                continue;

            db.Notifications.Add(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = item.UserId,
                Title = item.Title,
                Message = item.Message,
                Type = item.Type,
                IsRead = item.IsRead,
                CreatedAt = DateTime.UtcNow.AddHours(-item.Title.Length % 48)
            });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    private static async Task<DevelopmentSeedReport> BuildReportAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<SupplyRequest> supplyRequests)
    {
        var admin = await userManager.FindByEmailAsync("admin@gmail.com");

        var seedFactoryCount = await db.Factory.CountAsync(f =>
            f.User.Email != null && FactoryEmails.Contains(f.User.Email));
        var seedFarmCount = await db.Farm.CountAsync(f =>
            f.User.Email != null && FarmEmails.Contains(f.User.Email));
        var seedRequestCount = await db.SupplyRequests.CountAsync(r =>
            r.QualitySpecs != null && r.QualitySpecs.Contains(SeedMarker));
        var seedMatchCount = await db.FarmMatches.CountAsync(m =>
            m.SupplyRequest.QualitySpecs != null && m.SupplyRequest.QualitySpecs.Contains(SeedMarker));
        var seedContractCount = await db.Contracts.CountAsync(c =>
            c.GeneratedText != null && c.GeneratedText.Contains(SeedMarker));
        var seedMessageCount = await db.Messages.CountAsync(m => m.Content.Contains(SeedMarker));
        var seedNotificationCount = await db.Notifications.CountAsync(n => n.Title.Contains(SeedMarker));
        var seedReviewCount = await db.Reviews.CountAsync(r =>
            r.Comment != null && r.Comment.Contains(SeedMarker));
        var seedDocCount = await db.FarmDocuments.CountAsync(d => d.FileName.Contains(SeedMarker));

        return new DevelopmentSeedReport
        {
            AdminEmail = admin?.Email,
            AdminUserId = admin?.Id,
            FactoryEmails = FactoryEmails.ToList(),
            FarmEmails = FarmEmails.ToList(),
            Governorates = Governorates.ToList(),
            Users = 1 + FactoryEmails.Length + FarmEmails.Length,
            Factories = seedFactoryCount,
            Farms = seedFarmCount,
            SupplyRequests = seedRequestCount,
            FarmMatches = seedMatchCount,
            Contracts = seedContractCount,
            Messages = seedMessageCount,
            Notifications = seedNotificationCount,
            Reviews = seedReviewCount,
            CropTypes = await db.CropTypes.CountAsync(),
            Certifications = await db.Certifications.CountAsync(),
            FarmDocuments = seedDocCount,
            MarketPrices = await db.MarketPrices.CountAsync(p => p.Source == MarketPriceSource),
            SupplyRequestIds = supplyRequests
                .OrderBy(r => r.QualitySpecs)
                .Select(r => new SeedSupplyRequestInfo(
                    r.RequestId,
                    r.QualitySpecs ?? "",
                    r.Status.ToString(),
                    r.QuantityTons))
                .ToList(),
            Idempotent = true,
            Notes =
            [
                "Counts above are seed-owned rows only (marker/email keyed), not entire DB totals.",
                "Factory Capacity / PreferredCrops are not domain columns; encoded in IndustryType + SupplyRequest crops.",
                "Governorate is not an entity; seeded as string profiles + MarketPrice reference rows.",
                "ContractStatus has no Completed; seeded Draft, PendingSignature, Signed, Cancelled.",
                "FarmMatch statuses include Proposed (Pending), Accepted, Rejected, Expired."
            ]
        };
    }
}

public sealed record SeedSupplyRequestInfo(
    Guid RequestId,
    string QualitySpecs,
    string Status,
    decimal QuantityTons);

public sealed class DevelopmentSeedReport
{
    public string? AdminEmail { get; init; }
    public Guid? AdminUserId { get; init; }
    public List<string> FactoryEmails { get; init; } = [];
    public List<string> FarmEmails { get; init; } = [];
    public List<string> Governorates { get; init; } = [];
    public int Users { get; init; }
    public int Factories { get; init; }
    public int Farms { get; init; }
    public int SupplyRequests { get; init; }
    public int FarmMatches { get; init; }
    public int Contracts { get; init; }
    public int Messages { get; init; }
    public int Notifications { get; init; }
    public int Reviews { get; init; }
    public int CropTypes { get; init; }
    public int Certifications { get; init; }
    public int FarmDocuments { get; init; }
    public int MarketPrices { get; init; }
    public List<SeedSupplyRequestInfo> SupplyRequestIds { get; init; } = [];
    public bool Idempotent { get; init; }
    public List<string> Notes { get; init; } = [];

    public string ToSummary()
    {
        var lines = new List<string>
        {
            "",
            "======== NileChain Development Seed Report ========",
            $"Idempotent: {Idempotent}",
            $"Users: {Users}",
            $"Factories: {Factories}",
            $"Farms: {Farms}",
            $"SupplyRequests: {SupplyRequests}",
            $"FarmMatches: {FarmMatches}",
            $"Contracts: {Contracts}",
            $"Messages: {Messages}",
            $"Notifications: {Notifications}",
            $"Reviews: {Reviews}",
            $"CropTypes: {CropTypes}",
            $"Certifications: {Certifications}",
            $"FarmDocuments: {FarmDocuments}",
            $"MarketPrices (governorate refs): {MarketPrices}",
            $"Admin: {AdminEmail}",
            $"Factories: {string.Join(", ", FactoryEmails)}",
            $"Farms: {string.Join(", ", FarmEmails)}",
            $"Governorates: {string.Join(", ", Governorates)}",
            "SupplyRequest IDs (for AI runtime tests):"
        };

        foreach (var r in SupplyRequestIds)
            lines.Add($"  - {r.RequestId} | {r.Status} | {r.QuantityTons}t | {r.QualitySpecs}");

        foreach (var note in Notes)
            lines.Add($"Note: {note}");

        lines.Add("=================================================");
        lines.Add("");
        return string.Join(Environment.NewLine, lines);
    }
}
