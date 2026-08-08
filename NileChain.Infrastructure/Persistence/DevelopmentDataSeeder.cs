using NileChain.Domain.Entities;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NileChain.Infrastructure.Persistence;

/// <summary>
/// Idempotent development/demo sample data. Inserts entities only when missing.
/// Prefer running via <c>dotnet run -- --seed-demo</c> (not every app startup).
/// </summary>
public static partial class DevelopmentDataSeeder
{
    public static async Task<DevelopmentSeedReport> SeedAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger? logger = null)
    {
        var rng = new Random(2026);
        var phases = new List<SeedPhaseResult>();
        DevelopmentSeedReport? report = null;
        DemoSeedReport? demoReport = null;

        async Task RunPhaseAsync(string name, Func<Task> action)
        {
            logger?.LogInformation("Seed phase START: {Phase}", name);
            Console.WriteLine($"[SEED] START {name}");
            try
            {
                await action();
                phases.Add(new SeedPhaseResult(name, true, null));
                logger?.LogInformation("Seed phase OK: {Phase}", name);
                Console.WriteLine($"[SEED] OK    {name}");
            }
            catch (Exception ex)
            {
                phases.Add(new SeedPhaseResult(name, false, ex.Message));
                logger?.LogError(ex, "Seed phase FAILED: {Phase}", name);
                Console.WriteLine($"[SEED] FAIL  {name}: {ex.Message}");
                throw;
            }
        }

        try
        {
            List<CropType> cropTypes = [];
            List<Certification> certifications = [];
            List<Factory> factories = [];
            List<Farm> farms = [];
            List<SupplyRequest> supplyRequests = [];
            List<FarmMatch> matches = [];
            List<Contract> contracts = [];
            List<ApplicationUser> admins = [];
            var ragCount = 0;

            await RunPhaseAsync("CropTypes", async () =>
            {
                cropTypes = await SeedCropTypesAsync(db);
            });

            await RunPhaseAsync("Certifications", async () =>
            {
                certifications = await SeedCertificationsAsync(db);
            });

            await RunPhaseAsync("MarketPrices", async () =>
            {
                await SeedGovernorateMarketPricesAsync(db, cropTypes);
                db.ChangeTracker.Clear();
            });

            await RunPhaseAsync("Admins", async () =>
            {
                admins = await SeedAdminsAsync(userManager);
            });

            await RunPhaseAsync("Factories", async () =>
            {
                await SeedFactoriesAsync(db, userManager, rng);
                db.ChangeTracker.Clear();
                factories = await ReloadSeedFactoriesAsync(db);
            });

            await RunPhaseAsync("Farms", async () =>
            {
                cropTypes = await db.CropTypes.ToListAsync();
                certifications = await db.Certifications.ToListAsync();
                await SeedFarmsAsync(db, userManager, cropTypes, certifications, rng);
                db.ChangeTracker.Clear();
                farms = await ReloadSeedFarmsAsync(db);
            });

            await RunPhaseAsync("FarmDocuments", async () =>
            {
                var farmEmailSet = AllSeedFarmEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
                farms = await db.Farm
                    .Include(f => f.User)
                    .Include(f => f.FarmCertifications)
                    .Where(f => f.User.Email != null && farmEmailSet.Contains(f.User.Email))
                    .OrderBy(f => f.User.Email)
                    .ToListAsync();
                await SeedFarmDocumentsAsync(db, farms, rng);
                db.ChangeTracker.Clear();
                farms = await ReloadSeedFarmsAsync(db);
            });

            await RunPhaseAsync("SupplyRequests", async () =>
            {
                await SeedSupplyRequestsAsync(db, factories, cropTypes, rng);
                db.ChangeTracker.Clear();
                supplyRequests = await ReloadSeedRequestsAsync(db);
            });

            await RunPhaseAsync("FarmMatches", async () =>
            {
                await SeedFarmMatchesAsync(db, farms, supplyRequests, rng);
                db.ChangeTracker.Clear();
                matches = await ReloadSeedMatchesAsync(db);
            });

            await RunPhaseAsync("Contracts", async () =>
            {
                await SeedContractsAsync(db, matches, rng);
                db.ChangeTracker.Clear();
                contracts = await ReloadSeedContractsAsync(db);
                matches = await ReloadSeedMatchesAsync(db);
                factories = await ReloadSeedFactoriesAsync(db);
                farms = await ReloadSeedFarmsAsync(db);
            });

            await RunPhaseAsync("Messages", async () =>
            {
                await SeedMessagesAsync(db, matches, farms, factories, rng);
                db.ChangeTracker.Clear();
            });

            await RunPhaseAsync("Reviews", async () =>
            {
                await SeedReviewsAsync(db, contracts, matches, farms, factories, rng);
                db.ChangeTracker.Clear();
            });

            await RunPhaseAsync("RecalculateRatings", async () =>
            {
                await RecalculateRatingsAsync(db);
                db.ChangeTracker.Clear();
            });

            await RunPhaseAsync("Notifications", async () =>
            {
                await SeedNotificationsAsync(db, userManager, farms, factories, admins, rng);
                db.ChangeTracker.Clear();
            });

            await RunPhaseAsync("RagDocuments", async () =>
            {
                ragCount = await SeedRagDocumentsAsync(db, admins);
                db.ChangeTracker.Clear();
            });

            await RunPhaseAsync("DemoProfiles", async () =>
            {
                cropTypes = await db.CropTypes.ToListAsync();
                certifications = await db.Certifications.ToListAsync();
                demoReport = await SeedDemoProfilesAsync(db, userManager, cropTypes, certifications);
                db.ChangeTracker.Clear();
            });

            var qa = await RunQaChecksAsync(db);
            report = await BuildReportAsync(db, userManager, qa, ragCount);
            report.Phases = phases;
            logger?.LogInformation("{Report}", report.ToSummary());
            Console.WriteLine(report.ToSummary());
            if (demoReport is not null)
            {
                logger?.LogInformation("{DemoReport}", demoReport.ToSummary());
                Console.WriteLine(demoReport.ToSummary());
            }

            return report;
        }
        catch (Exception ex)
        {
            var failed = phases.Where(p => !p.Succeeded).Select(p => p.Name).ToList();
            var ok = phases.Where(p => p.Succeeded).Select(p => p.Name).ToList();
            var summary =
                $"[SEED] ABORTED after failure.\n" +
                $"Succeeded phases ({ok.Count}): {string.Join(", ", ok)}\n" +
                $"Failed phases ({failed.Count}): {string.Join(", ", failed)}\n" +
                $"Error: {ex.Message}";
            logger?.LogError(ex, "{Summary}", summary);
            Console.WriteLine(summary);
            throw;
        }
    }

    private static async Task<List<Factory>> ReloadSeedFactoriesAsync(NileChainDbContext db)
    {
        var emails = AllSeedFactoryEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await db.Factory
            .AsNoTracking()
            .Include(f => f.User)
            .Where(f => f.User.Email != null && emails.Contains(f.User.Email))
            .OrderBy(f => f.User.Email)
            .ToListAsync();
    }

    private static async Task<List<Farm>> ReloadSeedFarmsAsync(NileChainDbContext db)
    {
        var emails = AllSeedFarmEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await db.Farm
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Include(f => f.FarmCertifications)
            .Where(f => f.User.Email != null && emails.Contains(f.User.Email))
            .OrderBy(f => f.User.Email)
            .ToListAsync();
    }

    private static async Task<List<SupplyRequest>> ReloadSeedRequestsAsync(NileChainDbContext db)
    {
        return await db.SupplyRequests
            .AsNoTracking()
            .Where(r => r.QualitySpecs != null && r.QualitySpecs.Contains(SeedMarker))
            .OrderBy(r => r.QualitySpecs)
            .ToListAsync();
    }

    private static async Task<List<FarmMatch>> ReloadSeedMatchesAsync(NileChainDbContext db)
    {
        return await db.FarmMatches
            .AsNoTracking()
            .Where(m => m.SupplyRequest.QualitySpecs != null
                        && m.SupplyRequest.QualitySpecs.Contains(SeedMarker))
            .ToListAsync();
    }

    private static async Task<List<Contract>> ReloadSeedContractsAsync(NileChainDbContext db)
    {
        return await db.Contracts
            .AsNoTracking()
            .Where(c => c.GeneratedText != null && c.GeneratedText.Contains(SeedMarker))
            .ToListAsync();
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
            .Select(p => new { p.CropTypeId, p.Governorate, Day = p.RecordedAt.Date })
            .ToListAsync();

        var existingKeys = existing
            .Select(p => (p.CropTypeId, Gov: p.Governorate ?? "", p.Day))
            .ToHashSet();

        // ~6 months of weekly points for dashboard Chart.js trends
        var dateOffsets = new[]
        {
            0, 7, 14, 21, 28, 35, 42, 49, 56, 63, 70, 77, 84, 91, 105, 120, 135, 150, 165, 180
        };
        var added = false;
        var offset = 0;

        foreach (var gov in Governorates)
        {
            foreach (var crop in cropTypes.Where(c =>
                         CropNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase)))
            {
                var basePrice = BaseCropPrices.GetValueOrDefault(crop.Name, 9000m);

                foreach (var daysAgo in dateOffsets)
                {
                    var day = DateTime.UtcNow.Date.AddDays(-daysAgo);
                    if (existingKeys.Contains((crop.CropTypeId, gov, day)))
                        continue;

                    var variance = ((offset % 9) - 4) * 120m + (daysAgo / 7) * 50m;
                    db.MarketPrices.Add(new MarketPrice
                    {
                        PriceId = Guid.NewGuid(),
                        CropTypeId = crop.CropTypeId,
                        Governorate = gov,
                        PricePerTon = Math.Max(1000m, basePrice + variance),
                        Source = MarketPriceSource,
                        RecordedAt = day.AddHours(8 + offset % 10)
                    });
                    existingKeys.Add((crop.CropTypeId, gov, day));
                    added = true;
                    offset++;
                }
            }
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role,
        string phone,
        bool isVerified,
        bool isActive = true,
        bool emailConfirmed = true,
        int createdDaysAgo = 100,
        Guid? fixedUserId = null,
        string? password = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            Id = fixedUserId ?? Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            IsVerified = isVerified,
            IsActive = isActive,
            PhoneNumber = phone,
            CreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo)
        };

        var createResult = await userManager.CreateAsync(user, password ?? SeedPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(
                $"Failed to seed user {email}: " +
                string.Join("; ", createResult.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    // -------------------------------------------------------------------------
    // QA
    // -------------------------------------------------------------------------

    private static async Task<DevelopmentSeedQaResult> RunQaChecksAsync(NileChainDbContext db)
    {
        var errors = new List<string>();

        var orphanRequests = await db.SupplyRequests
            .CountAsync(r => !db.Factory.Any(f => f.FactoryId == r.FactoryId));
        if (orphanRequests > 0)
            errors.Add($"SupplyRequests missing factory FK: {orphanRequests}");

        var orphanMatches = await db.FarmMatches
            .CountAsync(m => !db.SupplyRequests.Any(r => r.RequestId == m.RequestId));
        if (orphanMatches > 0)
            errors.Add($"FarmMatches missing request FK: {orphanMatches}");

        var orphanContracts = await db.Contracts
            .CountAsync(c => !db.FarmMatches.Any(m => m.MatchId == c.MatchId));
        if (orphanContracts > 0)
            errors.Add($"Contracts missing match FK: {orphanContracts}");

        var orphanMessages = await db.Messages
            .CountAsync(m => !db.FarmMatches.Any(fm => fm.MatchId == m.MatchId));
        if (orphanMessages > 0)
            errors.Add($"Messages missing match FK: {orphanMessages}");

        var orphanNotifications = await db.Notifications
            .CountAsync(n => !db.Users.Any(u => u.Id == n.UserId));
        if (orphanNotifications > 0)
            errors.Add($"Notifications missing user FK: {orphanNotifications}");

        var duplicatePairs = await db.FarmMatches
            .GroupBy(m => new { m.RequestId, m.FarmId })
            .Where(g => g.Count() > 1)
            .CountAsync();
        if (duplicatePairs > 0)
            errors.Add($"Duplicate (RequestId, FarmId) pairs: {duplicatePairs}");

        var duplicateContractMatches = await db.Contracts
            .GroupBy(c => c.MatchId)
            .Where(g => g.Count() > 1)
            .CountAsync();
        if (duplicateContractMatches > 0)
            errors.Add($"Duplicate contracts per MatchId: {duplicateContractMatches}");

        return new DevelopmentSeedQaResult(
            Passed: errors.Count == 0,
            Errors: errors);
    }

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    private static async Task<DevelopmentSeedReport> BuildReportAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        DevelopmentSeedQaResult qa,
        int chromaDocuments)
    {
        var farmEmails = AllSeedFarmEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var factoryEmails = AllSeedFactoryEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seedFarms = await db.Farm
            .Include(f => f.User)
            .Where(f => f.User.Email != null && farmEmails.Contains(f.User.Email))
            .ToListAsync();
        var seedFactories = await db.Factory
            .Include(f => f.User)
            .Where(f => f.User.Email != null && factoryEmails.Contains(f.User.Email))
            .ToListAsync();

        var seedRequestCount = await db.SupplyRequests.CountAsync(r =>
            r.QualitySpecs != null && r.QualitySpecs.Contains(SeedMarker));
        var seedMatchCount = await db.FarmMatches.CountAsync(m =>
            m.SupplyRequest.QualitySpecs != null && m.SupplyRequest.QualitySpecs.Contains(SeedMarker));
        var seedContractCount = await db.Contracts.CountAsync(c =>
            c.GeneratedText != null && c.GeneratedText.Contains(SeedMarker));
        var seedMessageCount = await db.Messages.CountAsync(m => m.Content.Contains(SeedMarker));
        var seedNotificationCount = await db.Notifications.CountAsync(n =>
            n.Message.Contains(SeedMarker) || n.Title.Contains(SeedMarker));
        var seedReviewCount = await db.Reviews.CountAsync(r =>
            r.Comment != null && r.Comment.Contains(SeedMarker));
        var seedDocCount = await db.FarmDocuments.CountAsync(d => d.FileName.Contains(SeedMarker));
        var seedRagCount = await db.RagDocuments.CountAsync(d => d.Title.Contains(SeedMarker));
        var marketPriceCount = await db.MarketPrices.CountAsync(p => p.Source == MarketPriceSource);

        var avgFarmRating = seedFarms.Count == 0
            ? 0m
            : Math.Round(seedFarms.Average(f => f.AverageRating), 2);
        var avgRisk = seedFarms.Where(f => f.RiskScore.HasValue).Select(f => f.RiskScore!.Value).ToList();
        var avgRiskScore = avgRisk.Count == 0 ? 0m : Math.Round(avgRisk.Average(), 2);

        var matchScores = await db.FarmMatches
            .Where(m => m.SupplyRequest.QualitySpecs != null
                        && m.SupplyRequest.QualitySpecs.Contains(SeedMarker)
                        && m.MatchScore != null)
            .Select(m => m.MatchScore!.Value)
            .ToListAsync();
        var avgMatchScore = matchScores.Count == 0
            ? 0m
            : Math.Round(matchScores.Average(), 2);

        var govCovered = await db.Farm
            .Where(f => f.User.Email != null && farmEmails.Contains(f.User.Email) && f.Governorate != null)
            .Select(f => f.Governorate!)
            .Distinct()
            .CountAsync();
        var factoryGov = await db.Factory
            .Where(f => f.User.Email != null && factoryEmails.Contains(f.User.Email) && f.Governorate != null)
            .Select(f => f.Governorate!)
            .Distinct()
            .CountAsync();

        var superAdmin = await userManager.FindByEmailAsync(SuperAdminEmail);
        var adminUsers = 0;
        foreach (var email in AdminEmails)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
                adminUsers++;
        }

        var seedUserCount = (superAdmin is null ? 0 : 1) + adminUsers + seedFarms.Count + seedFactories.Count;

        return new DevelopmentSeedReport
        {
            SuperAdminEmail = superAdmin?.Email,
            AdminEmails = AdminEmails.ToList(),
            FactoryEmails = factoryEmails.OrderBy(e => e).ToList(),
            FarmEmails = farmEmails.OrderBy(e => e).ToList(),
            Governorates = Governorates.ToList(),
            Users = seedUserCount,
            Admins = adminUsers,
            SuperAdmins = superAdmin is null ? 0 : 1,
            Factories = seedFactories.Count,
            Farms = seedFarms.Count,
            SupplyRequests = seedRequestCount,
            FarmMatches = seedMatchCount,
            Contracts = seedContractCount,
            Messages = seedMessageCount,
            Notifications = seedNotificationCount,
            Reviews = seedReviewCount,
            CropTypes = await db.CropTypes.CountAsync(),
            Certifications = await db.Certifications.CountAsync(),
            FarmDocuments = seedDocCount,
            MarketPrices = marketPriceCount,
            ChromaDocuments = Math.Max(chromaDocuments, seedRagCount),
            AverageFarmRating = avgFarmRating,
            AverageMatchScore = avgMatchScore,
            AverageRiskScore = avgRiskScore,
            GovernoratesCovered = Math.Max(govCovered, factoryGov),
            CropTypesCovered = await db.CropTypes.CountAsync(c =>
                CropNames.Contains(c.Name)),
            QaPassed = qa.Passed,
            QaErrors = qa.Errors,
            Idempotent = true,
            Notes =
            [
                "Counts are seed-owned rows (marker/email keyed), not entire DB totals.",
                "Markers: [SEED] on bulk data; [DEMO] on fixed QA demo.* accounts.",
                "REQ-001..REQ-005 intentionally have zero matches for live agent demos.",
                "SupplyRequestStatus has no Expired; Cancelled covers withdrawn requests.",
                "ContractStatus has no Expired; Cancelled covers terminated contracts.",
                "Factory capacity is encoded in IndustryType (no Capacity column).",
                "ChromaDocuments reflects RagDocument rows; live Chroma ingest is best-effort.",
                $"Password for all seed.* accounts: {SeedPassword}",
                "Password for all demo.* accounts: Demo123@!"
            ]
        };
    }
}

public sealed record DevelopmentSeedQaResult(bool Passed, List<string> Errors);

public sealed record SeedPhaseResult(string Name, bool Succeeded, string? Error);

public sealed record SeedSupplyRequestInfo(
    Guid RequestId,
    string QualitySpecs,
    string Status,
    decimal QuantityTons);

public sealed class DevelopmentSeedReport
{
    public string? SuperAdminEmail { get; init; }
    public List<string> AdminEmails { get; init; } = [];
    public List<string> FactoryEmails { get; init; } = [];
    public List<string> FarmEmails { get; init; } = [];
    public List<string> Governorates { get; init; } = [];
    public int Users { get; init; }
    public int Admins { get; init; }
    public int SuperAdmins { get; init; }
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
    public int ChromaDocuments { get; init; }
    public decimal AverageFarmRating { get; init; }
    public decimal AverageMatchScore { get; init; }
    public decimal AverageRiskScore { get; init; }
    public int GovernoratesCovered { get; init; }
    public int CropTypesCovered { get; init; }
    public bool QaPassed { get; init; }
    public List<string> QaErrors { get; init; } = [];
    public bool Idempotent { get; init; }
    public List<string> Notes { get; init; } = [];
    public List<SeedPhaseResult> Phases { get; set; } = [];

    public string ToSummary()
    {
        var lines = new List<string>
        {
            "",
            "======== NileChain Development Seed Report ========",
            $"Idempotent: {Idempotent}",
            $"QA Passed: {QaPassed}",
            $"Users: {Users}",
            $"SuperAdmins: {SuperAdmins}",
            $"Admins: {Admins}",
            $"Factories: {Factories}",
            $"Farms: {Farms}",
            $"SupplyRequests: {SupplyRequests}",
            $"FarmMatches: {FarmMatches}",
            $"Contracts: {Contracts}",
            $"Messages: {Messages}",
            $"Notifications: {Notifications}",
            $"Reviews: {Reviews}",
            $"MarketPrices: {MarketPrices}",
            $"Chroma Documents: {ChromaDocuments}",
            $"CropTypes: {CropTypes}",
            $"Certifications: {Certifications}",
            $"FarmDocuments: {FarmDocuments}",
            $"Average Farm Rating: {AverageFarmRating}",
            $"Average Match Score: {AverageMatchScore}",
            $"Average Risk Score: {AverageRiskScore}",
            $"Governorates Covered: {GovernoratesCovered}",
            $"Crop Types Covered: {CropTypesCovered}",
            $"SuperAdmin: {SuperAdminEmail}",
            $"Admins: {string.Join(", ", AdminEmails)}",
            "Password (seed.*): Seed123@!",
            "Password (demo.*): Demo123@!"
        };

        if (Phases.Count > 0)
        {
            lines.Add("Phases:");
            foreach (var p in Phases)
                lines.Add($"  - {(p.Succeeded ? "OK" : "FAIL")} {p.Name}" +
                          (p.Error is null ? "" : $": {p.Error}"));
        }

        foreach (var err in QaErrors)
            lines.Add($"QA ERROR: {err}");

        foreach (var note in Notes)
            lines.Add($"Note: {note}");

        lines.Add("=================================================");
        lines.Add("");
        return string.Join(Environment.NewLine, lines);
    }
}
