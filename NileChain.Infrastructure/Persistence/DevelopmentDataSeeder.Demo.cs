using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

/// <summary>
/// Permanent demo / QA accounts. Emails, password, and entity IDs are fixed across runs.
/// </summary>
public static partial class DevelopmentDataSeeder
{
    private const string DemoPassword = "Demo123@!";
    private const string DemoMarker = "[DEMO]";

    // -------------------------------------------------------------------------
    // Fixed emails
    // -------------------------------------------------------------------------

    private const string DemoSuperAdminEmail = "demo.superadmin@nilechain.dev";
    private const string DemoAdmin1Email = "demo.admin1@nilechain.dev";
    private const string DemoAdmin2Email = "demo.admin2@nilechain.dev";

    private const string DemoFactoryRichEmail = "demo.factory.rich@nilechain.dev";
    private const string DemoFactoryAverageEmail = "demo.factory.average@nilechain.dev";
    private const string DemoFactoryEmptyEmail = "demo.factory.empty@nilechain.dev";
    private const string DemoFactoryInactiveEmail = "demo.factory.inactive@nilechain.dev";

    private const string DemoFarmTopEmail = "demo.farm.top@nilechain.dev";
    private const string DemoFarmAverageEmail = "demo.farm.average@nilechain.dev";
    private const string DemoFarmNewEmail = "demo.farm.new@nilechain.dev";
    private const string DemoFarmRiskyEmail = "demo.farm.risky@nilechain.dev";
    private const string DemoFarmUnverifiedEmail = "demo.farm.unverified@nilechain.dev";
    private const string DemoFarmInactiveEmail = "demo.farm.inactive@nilechain.dev";

    // -------------------------------------------------------------------------
    // Deterministic IDs (never change)
    // -------------------------------------------------------------------------

    private static class DemoIds
    {
        // Users
        public static readonly Guid SuperAdminUser = Guid.Parse("d0a00001-0001-4000-8000-000000000001");
        public static readonly Guid Admin1User = Guid.Parse("d0a00001-0001-4000-8000-000000000002");
        public static readonly Guid Admin2User = Guid.Parse("d0a00001-0001-4000-8000-000000000003");

        public static readonly Guid FactoryRichUser = Guid.Parse("d0a00002-0001-4000-8000-000000000001");
        public static readonly Guid FactoryAverageUser = Guid.Parse("d0a00002-0001-4000-8000-000000000002");
        public static readonly Guid FactoryEmptyUser = Guid.Parse("d0a00002-0001-4000-8000-000000000003");
        public static readonly Guid FactoryInactiveUser = Guid.Parse("d0a00002-0001-4000-8000-000000000004");

        public static readonly Guid FarmTopUser = Guid.Parse("d0a00003-0001-4000-8000-000000000001");
        public static readonly Guid FarmAverageUser = Guid.Parse("d0a00003-0001-4000-8000-000000000002");
        public static readonly Guid FarmNewUser = Guid.Parse("d0a00003-0001-4000-8000-000000000003");
        public static readonly Guid FarmRiskyUser = Guid.Parse("d0a00003-0001-4000-8000-000000000004");
        public static readonly Guid FarmUnverifiedUser = Guid.Parse("d0a00003-0001-4000-8000-000000000005");
        public static readonly Guid FarmInactiveUser = Guid.Parse("d0a00003-0001-4000-8000-000000000006");

        // Profiles
        public static readonly Guid FactoryRich = Guid.Parse("d0b00002-0001-4000-8000-000000000001");
        public static readonly Guid FactoryAverage = Guid.Parse("d0b00002-0001-4000-8000-000000000002");
        public static readonly Guid FactoryEmpty = Guid.Parse("d0b00002-0001-4000-8000-000000000003");
        public static readonly Guid FactoryInactive = Guid.Parse("d0b00002-0001-4000-8000-000000000004");

        public static readonly Guid FarmTop = Guid.Parse("d0b00003-0001-4000-8000-000000000001");
        public static readonly Guid FarmAverage = Guid.Parse("d0b00003-0001-4000-8000-000000000002");
        public static readonly Guid FarmNew = Guid.Parse("d0b00003-0001-4000-8000-000000000003");
        public static readonly Guid FarmRisky = Guid.Parse("d0b00003-0001-4000-8000-000000000004");
        public static readonly Guid FarmUnverified = Guid.Parse("d0b00003-0001-4000-8000-000000000005");
        public static readonly Guid FarmInactive = Guid.Parse("d0b00003-0001-4000-8000-000000000006");

        // Workflow A — Best AI Match (5 matches)
        public static readonly Guid ReqBestAi = Guid.Parse("d0c0000a-0001-4000-8000-000000000001");
        public static readonly Guid MatchATop = Guid.Parse("d0d0000a-0001-4000-8000-000000000001");
        public static readonly Guid MatchAAvg = Guid.Parse("d0d0000a-0001-4000-8000-000000000002");
        public static readonly Guid MatchANew = Guid.Parse("d0d0000a-0001-4000-8000-000000000003");
        public static readonly Guid MatchAUnv = Guid.Parse("d0d0000a-0001-4000-8000-000000000004");
        public static readonly Guid MatchARisk = Guid.Parse("d0d0000a-0001-4000-8000-000000000005");
        public static readonly Guid ContractASigned = Guid.Parse("d0e0000a-0001-4000-8000-000000000001");
        public static readonly Guid ContractADraft = Guid.Parse("d0e0000a-0001-4000-8000-000000000002");

        // Workflow B — No Matches
        public static readonly Guid ReqNoMatch = Guid.Parse("d0c0000b-0001-4000-8000-000000000001");

        // Workflow C — Pending / long conversation
        public static readonly Guid ReqPending = Guid.Parse("d0c0000c-0001-4000-8000-000000000001");
        public static readonly Guid MatchC1 = Guid.Parse("d0d0000c-0001-4000-8000-000000000001");
        public static readonly Guid MatchC2 = Guid.Parse("d0d0000c-0001-4000-8000-000000000002");
        public static readonly Guid ContractCPending = Guid.Parse("d0e0000c-0001-4000-8000-000000000001");

        // Extra rich-factory requests (dashboard volume + cancelled + medium/low)
        public static readonly Guid ReqCancelled = Guid.Parse("d0c0000a-0001-4000-8000-000000000002");
        public static readonly Guid ReqMedium = Guid.Parse("d0c0000a-0001-4000-8000-000000000003");
        public static readonly Guid ReqLow = Guid.Parse("d0c0000a-0001-4000-8000-000000000004");
        public static readonly Guid ReqFulfilled = Guid.Parse("d0c0000a-0001-4000-8000-000000000005");
        public static readonly Guid ReqMostConv = Guid.Parse("d0c0000a-0001-4000-8000-000000000006");

        public static readonly Guid MatchMed1 = Guid.Parse("d0d0000a-0001-4000-8000-000000000011");
        public static readonly Guid MatchLow1 = Guid.Parse("d0d0000a-0001-4000-8000-000000000012");
        public static readonly Guid MatchFulfilled = Guid.Parse("d0d0000a-0001-4000-8000-000000000013");
        public static readonly Guid MatchExpired = Guid.Parse("d0d0000a-0001-4000-8000-000000000014");
        public static readonly Guid MatchMostConv = Guid.Parse("d0d0000a-0001-4000-8000-000000000015");
        public static readonly Guid MatchSilent = Guid.Parse("d0d0000a-0001-4000-8000-000000000016");

        public static readonly Guid ContractCancelled = Guid.Parse("d0e0000a-0001-4000-8000-000000000003");
        public static readonly Guid ContractFulfilled = Guid.Parse("d0e0000a-0001-4000-8000-000000000004");
    }

    private static readonly string[] DemoRequestKeys =
    [
        "WF-A-BEST-AI",
        "WF-B-NO-MATCH",
        "WF-C-PENDING",
        "RICH-CANCELLED",
        "RICH-MEDIUM",
        "RICH-LOW",
        "RICH-FULFILLED",
        "RICH-MOST-CONV"
    ];

    public static async Task<DemoSeedReport> SeedDemoProfilesAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<CropType> cropTypes,
        List<Certification> certifications)
    {
        var wheat = RequireCrop(cropTypes, "Wheat");
        var corn = RequireCrop(cropTypes, "Corn");
        var tomato = RequireCrop(cropTypes, "Tomato");
        var rice = RequireCrop(cropTypes, "Rice");
        var potato = RequireCrop(cropTypes, "Potato");

        // --- Admins ---
        await EnsureDemoUserAsync(userManager, DemoSuperAdminEmail, AppRoles.SuperAdmin,
            "01080000001", true, true, true, 400, DemoIds.SuperAdminUser);
        await EnsureDemoUserAsync(userManager, DemoAdmin1Email, AppRoles.Admin,
            "01080000002", true, true, true, 380, DemoIds.Admin1User);
        await EnsureDemoUserAsync(userManager, DemoAdmin2Email, AppRoles.Admin,
            "01080000003", true, true, true, 370, DemoIds.Admin2User);

        // --- Factories ---
        var rich = await EnsureDemoFactoryAsync(db, userManager,
            DemoFactoryRichEmail, DemoIds.FactoryRichUser, DemoIds.FactoryRich,
            "Delta Rich Foods (Demo)", "10th of Ramadan", "Sharqia",
            "Food Processing | Capacity ~800 tons/month | Preferred: Wheat, Corn, Rice",
            verified: true, active: true, daysAgo: 200);

        var average = await EnsureDemoFactoryAsync(db, userManager,
            DemoFactoryAverageEmail, DemoIds.FactoryAverageUser, DemoIds.FactoryAverage,
            "Cairo Average Packing (Demo)", "6th of October", "Giza",
            "Canning & Packaging | Capacity ~250 tons/month | Preferred: Tomato, Potato",
            verified: true, active: true, daysAgo: 120);

        await EnsureDemoFactoryAsync(db, userManager,
            DemoFactoryEmptyEmail, DemoIds.FactoryEmptyUser, DemoIds.FactoryEmpty,
            "Empty Horizon Industries (Demo)", "Borg El Arab", "Alexandria",
            "Export Packing | Capacity ~100 tons/month | Preferred: Orange, Mango",
            verified: true, active: true, daysAgo: 30);

        await EnsureDemoFactoryAsync(db, userManager,
            DemoFactoryInactiveEmail, DemoIds.FactoryInactiveUser, DemoIds.FactoryInactive,
            "Inactive Nile Mills (Demo)", "Sadat City", "Monufia",
            "Flour Milling | Capacity ~180 tons/month | Preferred: Wheat",
            verified: false, active: false, daysAgo: 90);

        // --- Farms ---
        var top = await EnsureDemoFarmAsync(db, userManager, cropTypes, certifications,
            DemoFarmTopEmail, DemoIds.FarmTopUser, DemoIds.FarmTop,
            "Top Nile Valley Farm (Demo)", "Abu Sir", "Giza",
            size: 95m, soil: SoilType.Loamy, risk: 92m, verified: true, active: true,
            profileComplete: true, daysAgo: 300,
            crops: ["Wheat", "Corn", "Rice"],
            certs: ["Organic", "GlobalGAP", "ISO 22000"],
            expiredCerts: false,
            docs: true);

        var avgFarm = await EnsureDemoFarmAsync(db, userManager, cropTypes, certifications,
            DemoFarmAverageEmail, DemoIds.FarmAverageUser, DemoIds.FarmAverage,
            "Average Delta Farm (Demo)", "Zagazig", "Sharqia",
            size: 40m, soil: SoilType.Clay, risk: 68m, verified: true, active: true,
            profileComplete: true, daysAgo: 180,
            crops: ["Wheat", "Potato"],
            certs: ["GlobalGAP"],
            expiredCerts: false,
            docs: true);

        var newFarm = await EnsureDemoFarmAsync(db, userManager, cropTypes, certifications,
            DemoFarmNewEmail, DemoIds.FarmNewUser, DemoIds.FarmNew,
            "New Sunrise Fields (Demo)", "Damanhur", "Beheira",
            size: 12m, soil: SoilType.Sandy, risk: 55m, verified: true, active: true,
            profileComplete: false, daysAgo: 5,
            crops: ["Tomato"],
            certs: [],
            expiredCerts: false,
            docs: false);

        var risky = await EnsureDemoFarmAsync(db, userManager, cropTypes, certifications,
            DemoFarmRiskyEmail, DemoIds.FarmRiskyUser, DemoIds.FarmRisky,
            "Risky Red Earth Farm (Demo)", "Minya City", "Minya",
            size: 22m, soil: SoilType.Saline, risk: 18m, verified: true, active: true,
            profileComplete: true, daysAgo: 150,
            crops: ["Tomato", "Onion"],
            certs: ["Organic"],
            expiredCerts: true,
            docs: true);

        var unverified = await EnsureDemoFarmAsync(db, userManager, cropTypes, certifications,
            DemoFarmUnverifiedEmail, DemoIds.FarmUnverifiedUser, DemoIds.FarmUnverified,
            "Unverified Canal Farm (Demo)", "Desouk", "Kafr El Sheikh",
            size: 18m, soil: SoilType.Silty, risk: 48m, verified: false, active: true,
            profileComplete: false, daysAgo: 40,
            crops: ["Rice", "Corn"],
            certs: [],
            expiredCerts: false,
            docs: false);

        await EnsureDemoFarmAsync(db, userManager, cropTypes, certifications,
            DemoFarmInactiveEmail, DemoIds.FarmInactiveUser, DemoIds.FarmInactive,
            "Inactive Palm Grove Farm (Demo)", "Tanta", "Gharbia",
            size: 30m, soil: SoilType.Loamy, risk: 60m, verified: true, active: false,
            profileComplete: true, daysAgo: 220,
            crops: ["Wheat"],
            certs: ["HACCP"],
            expiredCerts: false,
            docs: true);

        await db.SaveChangesAsync();

        // =====================================================================
        // Workflow A — Rich: Best AI Match (5 matches, 2 Acc, 2 Prop, 1 Rej)
        // =====================================================================
        var reqBest = await EnsureDemoRequestAsync(db, DemoIds.ReqBestAi, rich.FactoryId, wheat.CropTypeId,
            "WF-A-BEST-AI", 80m, 12500m, SupplyRequestStatus.Matched, 35,
            "Sharqia", "Grade A wheat, moisture < 12%, protein > 11% — BEST AI DEMO");

        await EnsureDemoMatchAsync(db, DemoIds.MatchATop, reqBest.RequestId, top.FarmId,
            96m, 92m, FarmMatchStatus.Accepted, 20);
        await EnsureDemoMatchAsync(db, DemoIds.MatchAAvg, reqBest.RequestId, avgFarm.FarmId,
            88m, 68m, FarmMatchStatus.Accepted, 19);
        await EnsureDemoMatchAsync(db, DemoIds.MatchANew, reqBest.RequestId, newFarm.FarmId,
            72m, 55m, FarmMatchStatus.Proposed, 18);
        await EnsureDemoMatchAsync(db, DemoIds.MatchAUnv, reqBest.RequestId, unverified.FarmId,
            64m, 48m, FarmMatchStatus.Proposed, 17);
        await EnsureDemoMatchAsync(db, DemoIds.MatchARisk, reqBest.RequestId, risky.FarmId,
            31m, 18m, FarmMatchStatus.Rejected, 16);

        // Signed contract (Top) + Draft (Average accepted)
        await EnsureDemoContractAsync(db, DemoIds.ContractASigned, DemoIds.MatchATop,
            ContractStatus.Signed, 14);
        await EnsureDemoContractAsync(db, DemoIds.ContractADraft, DemoIds.MatchAAvg,
            ContractStatus.Draft, 12);

        // Active conversation on Top accepted match
        await EnsureDemoThreadAsync(db, DemoIds.MatchATop, rich.UserId, top.UserId, 8, shortThread: false);
        // Light conversation on Average
        await EnsureDemoThreadAsync(db, DemoIds.MatchAAvg, rich.UserId, avgFarm.UserId, 4, shortThread: true);

        // =====================================================================
        // Workflow B — Rich: No AI Matches
        // =====================================================================
        var reqNoMatch = await EnsureDemoRequestAsync(db, DemoIds.ReqNoMatch, rich.FactoryId, corn.CropTypeId,
            "WF-B-NO-MATCH", 45m, 9800m, SupplyRequestStatus.Pending, 50,
            "Aswan", "Specialty yellow corn — NO MATCHES DEMO (remote governorate)");

        // =====================================================================
        // Workflow C — Average factory: pending matches + long conversation + PendingSignature
        // =====================================================================
        var reqPending = await EnsureDemoRequestAsync(db, DemoIds.ReqPending, average.FactoryId, tomato.CropTypeId,
            "WF-C-PENDING", 30m, 7200m, SupplyRequestStatus.Matched, 25,
            "Beheira", "Processing tomatoes, firm, Brix 4.5+ — PENDING MATCHES DEMO");

        await EnsureDemoMatchAsync(db, DemoIds.MatchC1, reqPending.RequestId, newFarm.FarmId,
            79m, 55m, FarmMatchStatus.Accepted, 10);
        await EnsureDemoMatchAsync(db, DemoIds.MatchC2, reqPending.RequestId, avgFarm.FarmId,
            74m, 68m, FarmMatchStatus.Proposed, 9);

        await EnsureDemoContractAsync(db, DemoIds.ContractCPending, DemoIds.MatchC1,
            ContractStatus.PendingSignature, 7);

        await EnsureDemoThreadAsync(db, DemoIds.MatchC1, average.UserId, newFarm.UserId, 18, shortThread: false);

        // =====================================================================
        // Workflow D — Empty factory: intentionally no requests
        // =====================================================================
        // (no supply requests for demo.factory.empty)

        // =====================================================================
        // Extra rich-factory volume + QA scenarios
        // =====================================================================
        var reqCancelled = await EnsureDemoRequestAsync(db, DemoIds.ReqCancelled, rich.FactoryId, potato.CropTypeId,
            "RICH-CANCELLED", 60m, 8100m, SupplyRequestStatus.Cancelled, -10,
            "Minya", "Industrial potato — CANCELLED REQUEST DEMO");

        var reqMedium = await EnsureDemoRequestAsync(db, DemoIds.ReqMedium, rich.FactoryId, rice.CropTypeId,
            "RICH-MEDIUM", 55m, 11200m, SupplyRequestStatus.Matched, 40,
            "Dakahlia", "Egyptian short-grain rice — MEDIUM AI MATCH DEMO");
        await EnsureDemoMatchAsync(db, DemoIds.MatchMed1, reqMedium.RequestId, avgFarm.FarmId,
            66m, 68m, FarmMatchStatus.Proposed, 8);

        var reqLow = await EnsureDemoRequestAsync(db, DemoIds.ReqLow, rich.FactoryId, tomato.CropTypeId,
            "RICH-LOW", 20m, 6900m, SupplyRequestStatus.Matched, 28,
            "Matrouh", "Open-field tomato — LOW AI MATCH DEMO");
        await EnsureDemoMatchAsync(db, DemoIds.MatchLow1, reqLow.RequestId, risky.FarmId,
            28m, 18m, FarmMatchStatus.Proposed, 6);

        var reqFulfilled = await EnsureDemoRequestAsync(db, DemoIds.ReqFulfilled, rich.FactoryId, wheat.CropTypeId,
            "RICH-FULFILLED", 70m, 11800m, SupplyRequestStatus.Fulfilled, -25,
            "Giza", "Bread wheat fulfilled — SIGNED + REVIEWS DEMO");
        await EnsureDemoMatchAsync(db, DemoIds.MatchFulfilled, reqFulfilled.RequestId, top.FarmId,
            94m, 92m, FarmMatchStatus.Accepted, 40);
        await EnsureDemoMatchAsync(db, DemoIds.MatchExpired, reqFulfilled.RequestId, unverified.FarmId,
            52m, 48m, FarmMatchStatus.Expired, 38);
        await EnsureDemoContractAsync(db, DemoIds.ContractFulfilled, DemoIds.MatchFulfilled,
            ContractStatus.Signed, 30);

        var reqMostConv = await EnsureDemoRequestAsync(db, DemoIds.ReqMostConv, rich.FactoryId, corn.CropTypeId,
            "RICH-MOST-CONV", 40m, 9600m, SupplyRequestStatus.Matched, 22,
            "Beheira", "Yellow corn — MOST CONVERSATIONS DEMO");
        await EnsureDemoMatchAsync(db, DemoIds.MatchMostConv, reqMostConv.RequestId, avgFarm.FarmId,
            85m, 68m, FarmMatchStatus.Accepted, 15);
        await EnsureDemoMatchAsync(db, DemoIds.MatchSilent, reqMostConv.RequestId, top.FarmId,
            90m, 92m, FarmMatchStatus.Accepted, 14);
        await EnsureDemoThreadAsync(db, DemoIds.MatchMostConv, rich.UserId, avgFarm.UserId, 22, shortThread: false);
        // MatchSilent intentionally has zero messages (No Conversations scenario)

        // Cancelled contract on a dedicated draft→cancelled: use MatchSilent with Cancelled contract
        await EnsureDemoContractAsync(db, DemoIds.ContractCancelled, DemoIds.MatchSilent,
            ContractStatus.Cancelled, 10);

        await db.SaveChangesAsync();

        // Reviews for signed contracts (top farm excellent rating)
        await EnsureDemoReviewsAsync(db, DemoIds.ContractASigned, rich.UserId, top.UserId, 5, 5);
        await EnsureDemoReviewsAsync(db, DemoIds.ContractFulfilled, rich.UserId, top.UserId, 5, 4);

        // Risky farm poor rating via a synthetic review on fulfilled if we have a contract — use ContractASigned target risky? No.
        // Seed a review where factory reviews risky if there's a contract — we don't have one.
        // Set AverageRating directly on risky/top farms for demo stability.
        await ApplyDemoRatingsAsync(db);

        // Notifications: many for rich + top; none for empty factory
        await SeedDemoNotificationsAsync(db, rich.UserId, top.UserId, average.UserId, newFarm.UserId);

        await db.SaveChangesAsync();

        return await BuildDemoReportAsync(db);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static CropType RequireCrop(List<CropType> crops, string name) =>
        crops.First(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static async Task<ApplicationUser> EnsureDemoUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role,
        string phone,
        bool isVerified,
        bool isActive,
        bool emailConfirmed,
        int daysAgo,
        Guid fixedId) =>
        await EnsureUserAsync(
            userManager, email, role, phone, isVerified, isActive, emailConfirmed, daysAgo,
            fixedUserId: fixedId, password: DemoPassword);

    private static async Task<Factory> EnsureDemoFactoryAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        string email,
        Guid userId,
        Guid factoryId,
        string name,
        string location,
        string governorate,
        string industry,
        bool verified,
        bool active,
        int daysAgo)
    {
        var existing = await db.Factory
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.User.Email == email);
        if (existing is not null)
            return existing;

        var user = await EnsureDemoUserAsync(
            userManager, email, AppRoles.Factory, PhoneFromGuid(userId),
            verified, active, emailConfirmed: true, daysAgo, userId);

        var factory = new Factory
        {
            FactoryId = factoryId,
            UserId = user.Id,
            Name = name,
            Location = location,
            Governorate = governorate,
            IndustryType = industry,
            IsVerified = verified,
            AverageRating = 0m,
            RatingCount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
        db.Factory.Add(factory);
        return factory;
    }

    private static async Task<Farm> EnsureDemoFarmAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<CropType> cropTypes,
        List<Certification> certifications,
        string email,
        Guid userId,
        Guid farmId,
        string name,
        string location,
        string governorate,
        decimal size,
        SoilType soil,
        decimal risk,
        bool verified,
        bool active,
        bool profileComplete,
        int daysAgo,
        string[] crops,
        string[] certs,
        bool expiredCerts,
        bool docs)
    {
        var existing = await db.Farm
            .Include(f => f.User)
            .Include(f => f.FarmCrops)
            .Include(f => f.FarmCertifications)
            .FirstOrDefaultAsync(f => f.User.Email == email);
        if (existing is not null)
            return existing;

        var user = await EnsureDemoUserAsync(
            userManager, email, AppRoles.Farm, PhoneFromGuid(userId),
            verified, active, emailConfirmed: true, daysAgo, userId);

        var farm = new Farm
        {
            FarmId = farmId,
            UserId = user.Id,
            Name = name,
            Location = location,
            Governorate = governorate,
            SizeInFeddans = size,
            SoilType = soil,
            RiskScore = risk,
            IsVerified = verified,
            ProfileComplete = profileComplete,
            AverageRating = 0m,
            RatingCount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };

        foreach (var cropName in crops)
        {
            var crop = cropTypes.First(c => c.Name.Equals(cropName, StringComparison.OrdinalIgnoreCase));
            farm.FarmCrops.Add(new FarmCrop
            {
                FarmId = farmId,
                CropTypeId = crop.CropTypeId,
                AvailableQuantityTons = size * 2m,
                AvailableFrom = DateTime.UtcNow.Date.AddMonths(-1),
                AvailableTo = DateTime.UtcNow.Date.AddMonths(6),
                MinPricePerTon = 8000m,
                CropType = crop
            });
        }

        for (var i = 0; i < certs.Length; i++)
        {
            var cert = certifications.First(c => c.Name.Equals(certs[i], StringComparison.OrdinalIgnoreCase));
            farm.FarmCertifications.Add(new FarmCertification
            {
                FarmId = farmId,
                CertificationId = cert.CertificationId,
                IssuedAt = expiredCerts
                    ? DateTime.UtcNow.AddYears(-2)
                    : DateTime.UtcNow.AddMonths(-8),
                ExpiresAt = expiredCerts
                    ? DateTime.UtcNow.AddMonths(-3)
                    : DateTime.UtcNow.AddMonths(10 + i)
            });
        }

        db.Farm.Add(farm);

        if (docs)
        {
            db.FarmDocuments.Add(new FarmDocument
            {
                FarmDocumentId = CreateDeterministicGuid($"demo-doc-{farmId:N}-land"),
                FarmId = farmId,
                FileName = $"{DemoMarker} land-title-{email.Split('@')[0]}.pdf",
                FileUrl = $"https://res.cloudinary.com/demo/raw/upload/demo/{farmId:N}/land-title.pdf",
                FileSize = 200_000,
                FileType = "application/pdf",
                PublicId = $"demo/{farmId:N}/land-title",
                UploadedAt = DateTime.UtcNow.AddDays(-Math.Max(1, daysAgo / 2))
            });
        }

        return farm;
    }

    private static string PhoneFromGuid(Guid id)
    {
        var n = Math.Abs(id.GetHashCode() % 90_000_000);
        return $"010{n:D8}"[..11];
    }

    private static async Task<SupplyRequest> EnsureDemoRequestAsync(
        NileChainDbContext db,
        Guid requestId,
        Guid factoryId,
        Guid cropTypeId,
        string key,
        decimal qty,
        decimal price,
        SupplyRequestStatus status,
        int deliveryDays,
        string gov,
        string specs)
    {
        var marker = $"{DemoMarker}:{key}";
        var existing = await db.SupplyRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId
                                      || (r.QualitySpecs != null && r.QualitySpecs.Contains(marker)));
        if (existing is not null)
            return existing;

        var request = new SupplyRequest
        {
            RequestId = requestId,
            FactoryId = factoryId,
            CropTypeId = cropTypeId,
            QuantityTons = qty,
            QualitySpecs = $"{marker} | Gov:{gov} | {specs}",
            PricePerTon = price,
            DeliveryDate = DateTime.UtcNow.AddDays(deliveryDays),
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-(Math.Abs(deliveryDays) + 5))
        };
        db.SupplyRequests.Add(request);
        return request;
    }

    private static async Task EnsureDemoMatchAsync(
        NileChainDbContext db,
        Guid matchId,
        Guid requestId,
        Guid farmId,
        decimal matchScore,
        decimal riskScore,
        FarmMatchStatus status,
        int daysAgo)
    {
        if (await db.FarmMatches.AnyAsync(m => m.MatchId == matchId
                                              || (m.RequestId == requestId && m.FarmId == farmId)))
            return;

        db.FarmMatches.Add(new FarmMatch
        {
            MatchId = matchId,
            RequestId = requestId,
            FarmId = farmId,
            MatchScore = matchScore,
            RiskScore = riskScore,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        });
    }

    private static async Task EnsureDemoContractAsync(
        NileChainDbContext db,
        Guid contractId,
        Guid matchId,
        ContractStatus status,
        int daysAgo)
    {
        if (await db.Contracts.AnyAsync(c => c.ContractId == contractId || c.MatchId == matchId))
            return;

        db.Contracts.Add(new Contract
        {
            ContractId = contractId,
            MatchId = matchId,
            GeneratedText =
                $"{DemoMarker} Demo supply contract for match {matchId:N}. Status={status}. " +
                "Egyptian civil code. Quality, quantity, delivery, and payment terms included.",
            PdfUrl = status is ContractStatus.Signed or ContractStatus.PendingSignature
                ? $"https://res.cloudinary.com/demo/raw/upload/demo/contracts/{matchId:N}.pdf"
                : null,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
            SignedAt = status == ContractStatus.Signed
                ? DateTime.UtcNow.AddDays(-(daysAgo - 2))
                : null,
            FactorySignedAt = status switch
            {
                ContractStatus.Signed => DateTime.UtcNow.AddDays(-(daysAgo - 1)),
                ContractStatus.PendingFarmSignature => DateTime.UtcNow.AddDays(-(daysAgo - 1)),
                _ => null
            },
            FarmSignedAt = status switch
            {
                ContractStatus.Signed => DateTime.UtcNow.AddDays(-(daysAgo - 2)),
                ContractStatus.PendingFactorySignature => DateTime.UtcNow.AddDays(-(daysAgo - 1)),
                _ => null
            }
        });
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        hash[7] = (byte)((hash[7] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash);
    }

    private static async Task EnsureDemoThreadAsync(
        NileChainDbContext db,
        Guid matchId,
        Guid factoryUserId,
        Guid farmUserId,
        int messageCount,
        bool shortThread)
    {
        _ = shortThread;
        var prefix = $"{DemoMarker}:MSG-{matchId:N}-";
        if (await db.Messages.AnyAsync(m => m.MatchId == matchId && m.Content.StartsWith(prefix)))
            return;

        for (var i = 0; i < messageCount; i++)
        {
            var fromFactory = i % 2 == 0;
            var template = fromFactory
                ? MessageTemplatesFactory[i % MessageTemplatesFactory.Length]
                : MessageTemplatesFarm[i % MessageTemplatesFarm.Length];

            db.Messages.Add(new Message
            {
                MessageId = CreateDeterministicGuid($"demo-msg-{matchId:N}-{i:D3}"),
                MatchId = matchId,
                SenderId = fromFactory ? factoryUserId : farmUserId,
                ReceiverId = fromFactory ? farmUserId : factoryUserId,
                Content = $"{prefix}{i:D2} {template}",
                IsRead = i < messageCount - 2,
                CreatedAt = DateTime.UtcNow.AddDays(-(messageCount - i)).AddHours(i)
            });
        }
    }

    private static async Task EnsureDemoReviewsAsync(
        NileChainDbContext db,
        Guid contractId,
        Guid factoryUserId,
        Guid farmUserId,
        int factoryRatesFarm,
        int farmRatesFactory)
    {
        if (await db.Reviews.AnyAsync(r => r.ContractId == contractId && r.Comment != null && r.Comment.Contains(DemoMarker)))
            return;

        db.Reviews.Add(new Review
        {
            ReviewId = CreateDeterministicGuid($"demo-review-{contractId:N}-factory"),
            ContractId = contractId,
            ReviewerId = factoryUserId,
            TargetId = farmUserId,
            Rating = factoryRatesFarm,
            Comment = $"{DemoMarker} Excellent harvest quality and on-time delivery.",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });

        db.Reviews.Add(new Review
        {
            ReviewId = CreateDeterministicGuid($"demo-review-{contractId:N}-farm"),
            ContractId = contractId,
            ReviewerId = farmUserId,
            TargetId = factoryUserId,
            Rating = farmRatesFactory,
            Comment = $"{DemoMarker} Fair terms and professional communication.",
            CreatedAt = DateTime.UtcNow.AddDays(-9)
        });
    }

    private static async Task ApplyDemoRatingsAsync(NileChainDbContext db)
    {
        var top = await db.Farm.FirstOrDefaultAsync(f => f.FarmId == DemoIds.FarmTop);
        if (top is not null)
        {
            top.AverageRating = 4.9m;
            top.RatingCount = Math.Max(top.RatingCount, 8);
        }

        var risky = await db.Farm.FirstOrDefaultAsync(f => f.FarmId == DemoIds.FarmRisky);
        if (risky is not null)
        {
            risky.AverageRating = 1.6m;
            risky.RatingCount = Math.Max(risky.RatingCount, 5);
        }

        var avg = await db.Farm.FirstOrDefaultAsync(f => f.FarmId == DemoIds.FarmAverage);
        if (avg is not null && avg.AverageRating == 0)
        {
            avg.AverageRating = 3.7m;
            avg.RatingCount = 3;
        }

        var rich = await db.Factory.FirstOrDefaultAsync(f => f.FactoryId == DemoIds.FactoryRich);
        if (rich is not null)
        {
            rich.AverageRating = 4.6m;
            rich.RatingCount = Math.Max(rich.RatingCount, 6);
        }
    }

    private static async Task SeedDemoNotificationsAsync(
        NileChainDbContext db,
        Guid richUserId,
        Guid topFarmUserId,
        Guid averageFactoryUserId,
        Guid newFarmUserId)
    {
        var items = new List<(Guid UserId, string Key, string Title, string Message, string Type, bool Read)>
        {
            (richUserId, "N01", "AI matches ready", "5 farms matched for your wheat supply request.", "Match", false),
            (richUserId, "N02", "Contract signed", "Top Nile Valley Farm signed the wheat contract.", "Contract", false),
            (richUserId, "N03", "New message", "You have unread messages on an active match.", "Message", false),
            (richUserId, "N04", "Match rejected", "Risky Red Earth Farm rejected a proposal.", "Match", true),
            (richUserId, "N05", "Delivery reminder", "Upcoming delivery window within 7 days.", "Contract", false),
            (richUserId, "N06", "Market alert", "Wheat prices shifted in Sharqia this week.", "System", true),
            (richUserId, "N07", "Review received", "You received a new factory rating.", "Review", false),
            (richUserId, "N08", "Profile tip", "Keep preferred crops updated for better AI matches.", "System", true),
            (topFarmUserId, "N09", "Match accepted", "You accepted a match from Delta Rich Foods.", "Match", false),
            (topFarmUserId, "N10", "Contract signed", "Your wheat supply contract is signed.", "Contract", false),
            (topFarmUserId, "N11", "New message", "Delta Rich Foods sent a new message.", "Message", false),
            (topFarmUserId, "N12", "Payment note", "Awaiting payment confirmation after delivery.", "Payment", true),
            (averageFactoryUserId, "N13", "Pending signature", "Contract awaiting signature with New Sunrise Fields.", "Contract", false),
            (newFarmUserId, "N14", "Long conversation", "Continue the tomato supply discussion.", "Message", false),
            // Empty factory intentionally omitted — No Notifications scenario
        };

        foreach (var item in items)
        {
            var title = $"{DemoMarker} {item.Title} [{item.Key}]";
            if (await db.Notifications.AnyAsync(n => n.UserId == item.UserId && n.Title == title))
                continue;

            db.Notifications.Add(new Notification
            {
                NotificationId = CreateDeterministicGuid($"demo-notif-{item.Key}-{item.UserId:N}"),
                UserId = item.UserId,
                Title = title,
                Message = item.Message,
                Type = item.Type,
                IsRead = item.Read,
                CreatedAt = DateTime.UtcNow.AddHours(-(item.Key[2] - '0' + 1) * 6)
            });
        }
    }

    private static async Task<DemoSeedReport> BuildDemoReportAsync(NileChainDbContext db)
    {
        async Task<Guid?> FindRequestId(string key)
        {
            var marker = $"{DemoMarker}:{key}";
            var id = await db.SupplyRequests
                .Where(r => r.QualitySpecs != null && r.QualitySpecs.Contains(marker))
                .Select(r => (Guid?)r.RequestId)
                .FirstOrDefaultAsync();
            return id;
        }

        return new DemoSeedReport
        {
            Password = DemoPassword,
            SuperAdminEmail = DemoSuperAdminEmail,
            AdminEmails = [DemoAdmin1Email, DemoAdmin2Email],
            RichFactoryEmail = DemoFactoryRichEmail,
            AverageFactoryEmail = DemoFactoryAverageEmail,
            EmptyFactoryEmail = DemoFactoryEmptyEmail,
            InactiveFactoryEmail = DemoFactoryInactiveEmail,
            TopFarmEmail = DemoFarmTopEmail,
            AverageFarmEmail = DemoFarmAverageEmail,
            NewFarmEmail = DemoFarmNewEmail,
            RiskyFarmEmail = DemoFarmRiskyEmail,
            UnverifiedFarmEmail = DemoFarmUnverifiedEmail,
            InactiveFarmEmail = DemoFarmInactiveEmail,
            BestAiMatchRequestId = await FindRequestId("WF-A-BEST-AI"),
            NoMatchesRequestId = await FindRequestId("WF-B-NO-MATCH"),
            MostConversationsRequestId = await FindRequestId("RICH-MOST-CONV"),
            PendingMatchesRequestId = await FindRequestId("WF-C-PENDING"),
            CancelledRequestId = await FindRequestId("RICH-CANCELLED"),
            MediumMatchRequestId = await FindRequestId("RICH-MEDIUM"),
            LowMatchRequestId = await FindRequestId("RICH-LOW"),
            FulfilledRequestId = await FindRequestId("RICH-FULFILLED")
        };
    }
}

public sealed class DemoSeedReport
{
    public string Password { get; init; } = "Demo123@!";
    public string SuperAdminEmail { get; init; } = "";
    public List<string> AdminEmails { get; init; } = [];
    public string RichFactoryEmail { get; init; } = "";
    public string AverageFactoryEmail { get; init; } = "";
    public string EmptyFactoryEmail { get; init; } = "";
    public string InactiveFactoryEmail { get; init; } = "";
    public string TopFarmEmail { get; init; } = "";
    public string AverageFarmEmail { get; init; } = "";
    public string NewFarmEmail { get; init; } = "";
    public string RiskyFarmEmail { get; init; } = "";
    public string UnverifiedFarmEmail { get; init; } = "";
    public string InactiveFarmEmail { get; init; } = "";
    public Guid? BestAiMatchRequestId { get; init; }
    public Guid? NoMatchesRequestId { get; init; }
    public Guid? MostConversationsRequestId { get; init; }
    public Guid? PendingMatchesRequestId { get; init; }
    public Guid? CancelledRequestId { get; init; }
    public Guid? MediumMatchRequestId { get; init; }
    public Guid? LowMatchRequestId { get; init; }
    public Guid? FulfilledRequestId { get; init; }

    public string ToSummary()
    {
        var lines = new List<string>
        {
            "",
            "==============================",
            "DEMO ACCOUNTS",
            "==============================",
            "",
            "Password (all demo accounts):",
            Password,
            "",
            "Super Admin",
            SuperAdminEmail,
            "",
            "Admins",
            string.Join(Environment.NewLine, AdminEmails),
            "",
            "Rich Factory",
            RichFactoryEmail,
            "",
            "Average Factory",
            AverageFactoryEmail,
            "",
            "Empty Factory",
            EmptyFactoryEmail,
            "",
            "Inactive Factory",
            InactiveFactoryEmail,
            "",
            "Top Farm",
            TopFarmEmail,
            "",
            "Average Farm",
            AverageFarmEmail,
            "",
            "New Farm",
            NewFarmEmail,
            "",
            "Risky Farm",
            RiskyFarmEmail,
            "",
            "Unverified Farm",
            UnverifiedFarmEmail,
            "",
            "Inactive Farm",
            InactiveFarmEmail,
            "",
            "==============================",
            "BEST DEMO REQUESTS",
            "==============================",
            "",
            $"Best AI Match:        {BestAiMatchRequestId}",
            $"No Matches:           {NoMatchesRequestId}",
            $"Most Conversations:   {MostConversationsRequestId}",
            $"Pending Matches:      {PendingMatchesRequestId}",
            $"Cancelled Request:    {CancelledRequestId}",
            $"Medium AI Match:      {MediumMatchRequestId}",
            $"Low AI Match:         {LowMatchRequestId}",
            $"Fulfilled + Signed:   {FulfilledRequestId}",
            "",
            "==============================",
            "RECOMMENDED DEMO FLOW",
            "==============================",
            "",
            "1. Login as Rich Factory",
            $"   {RichFactoryEmail}",
            "2. Open Supply Request (Best AI Match)",
            $"   {BestAiMatchRequestId}",
            "3. Review AI Farm Matches (High / Medium / Low scores)",
            "4. Open Factory Matches — Accepted / Proposed / Rejected",
            "5. Login as Top Farm",
            $"   {TopFarmEmail}",
            "6. Open the Accepted Match + Messages",
            "7. Open the Signed Contract",
            "8. Verify Notifications on both accounts",
            "",
            "Empty Dashboard:",
            $"   Login {EmptyFactoryEmail} (no requests / no matches / no notifications)",
            "",
            "Pending Signature + Long Conversation:",
            $"   Login {AverageFactoryEmail} -> Request {PendingMatchesRequestId}",
            "",
            "High Risk / Poor Rating / Expired Certs:",
            $"   Login {RiskyFarmEmail}",
            "",
            "Brand-new farm (no contracts / no certs):",
            $"   Login {NewFarmEmail}",
            "",
            "No AI Matches:",
            $"   Rich Factory -> Request {NoMatchesRequestId}",
            "",
            "==============================",
            ""
        };

        return string.Join(Environment.NewLine, lines);
    }
}
