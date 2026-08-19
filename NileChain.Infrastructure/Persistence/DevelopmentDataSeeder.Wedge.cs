using NileChain.Domain.Common;
using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

/// <summary>
/// Demo density for the Tomato × Qalyubia wedge. Separate from nationwide [SEED]/[DEMO].
/// Fake customers so Exact RFQ is not empty in demo — not real GTM.
/// </summary>
public static partial class DevelopmentDataSeeder
{
    private const string WedgeMarker = "[WEDGE]";
    private const string WedgePassword = "Demo123@!";
    internal const int WedgeFarmCount = 20;
    internal const int WedgeFactoryCount = 5;

    private static class WedgeIds
    {
        public static Guid FarmUser(int i) =>
            Guid.Parse($"e0a00003-0001-4000-8000-{i:D12}");

        public static Guid Farm(int i) =>
            Guid.Parse($"e0b00003-0001-4000-8000-{i:D12}");

        public static Guid FactoryUser(int i) =>
            Guid.Parse($"e0a00002-0001-4000-8000-{i:D12}");

        public static Guid Factory(int i) =>
            Guid.Parse($"e0b00002-0001-4000-8000-{i:D12}");

        public static readonly Guid ExactRequest = Guid.Parse("e0c0000a-0001-4000-8000-000000000001");
        public static readonly Guid NearbyRequest = Guid.Parse("e0c0000a-0001-4000-8000-000000000002");
    }

    private static string WedgeFarmEmail(int i) => $"wedge.farm.{i:D2}@nilechain.dev";
    private static string WedgeFactoryEmail(int i) => $"wedge.factory.{i:D2}@nilechain.dev";

    private static readonly (string Name, string Governorate, string Location, string Industry, decimal Lat, decimal Lon)[]
        WedgeFactories =
        [
            ("Banha Tomato Paste Co. [WEDGE]", "Qalyubia", "Banha industrial zone", "Tomato Paste", 30.4667m, 31.1833m),
            ("10th of Ramadan Canning [WEDGE]", "Sharqia", "10th of Ramadan", "Canning & Packaging", 30.304m, 31.741m),
            ("Obour Juice Plant [WEDGE]", "Qalyubia", "El-Obour", "Dairy & Juice", 30.228m, 31.477m),
            ("Cairo-edge Frozen Foods [WEDGE]", "Cairo", "Cairo outskirts", "Frozen Vegetables", 30.12m, 31.32m),
            ("Qalyub Paste Line 2 [WEDGE]", "Qalyubia", "Qalyub", "Tomato Paste", 30.179m, 31.205m)
        ];

    private static async Task SeedWedgeTomatoQalyubiaAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<CropType> cropTypes,
        List<Certification> certifications,
        Guid adminUserId)
    {
        var tomato = cropTypes.FirstOrDefault(c =>
            c.Name.Equals("Tomato", StringComparison.OrdinalIgnoreCase));
        if (tomato is null)
            throw new InvalidOperationException("Wedge seed requires CropType Tomato.");

        var globalGap = certifications.FirstOrDefault(c =>
            c.Name.Equals("GlobalGAP", StringComparison.OrdinalIgnoreCase))
            ?? certifications.FirstOrDefault();

        EgyptGovernorateCentroids.TryResolve("Qalyubia", out var qLat, out var qLon);

        for (var i = 1; i <= WedgeFarmCount; i++)
        {
            var email = WedgeFarmEmail(i);
            var exists = await db.Farm.AnyAsync(f => f.User.Email == email);
            if (exists)
                continue;

            var user = await EnsureUserAsync(
                userManager,
                email,
                AppRoles.Farm,
                $"01550{i:D6}",
                isVerified: true,
                isActive: true,
                emailConfirmed: true,
                createdDaysAgo: 40 + i,
                fixedUserId: WedgeIds.FarmUser(i),
                password: WedgePassword);

            var lat = (decimal)qLat + (i % 5) * 0.012m;
            var lon = (decimal)qLon + (i % 4) * 0.014m;
            var tons = 80m + i * 6m;
            var minPrice = 6200m + (i % 5) * 100m;

            var farm = new Farm
            {
                FarmId = WedgeIds.Farm(i),
                UserId = user.Id,
                Name = $"مزرعة طماطم القليوبية {i:D2} {WedgeMarker}",
                Location = i % 2 == 0 ? "Banha" : "Qalyub",
                Governorate = "Qalyubia",
                Latitude = lat,
                Longitude = lon,
                SizeInFeddans = 40m + i * 3m,
                SoilType = SoilType.Clay,
                Description = $"{WedgeMarker} Demo density only — not a real customer.",
                RiskScore = 72m + (i % 10),
                IsVerified = true,
                ProfileComplete = true,
                BankName = "NBE",
                AccountHolderName = $"Wedge Farm {i:D2}",
                BankAccountNumber = $"000{i:D10}",
                AverageRating = 0m,
                RatingCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-(30 + i))
            };

            farm.FarmCrops.Add(new FarmCrop
            {
                FarmId = farm.FarmId,
                CropTypeId = tomato.CropTypeId,
                AvailableQuantityTons = tons,
                AvailableFrom = DateTime.UtcNow.Date.AddMonths(-2),
                AvailableTo = DateTime.UtcNow.Date.AddMonths(6),
                MinPricePerTon = minPrice,
                IsPublished = true,
                CropType = tomato
            });

            if (globalGap is not null)
            {
                farm.FarmCertifications.Add(new FarmCertification
                {
                    FarmId = farm.FarmId,
                    CertificationId = globalGap.CertificationId,
                    IssuedAt = DateTime.UtcNow.AddMonths(-8),
                    ExpiresAt = DateTime.UtcNow.AddMonths(10),
                    GrantedByAdminUserId = adminUserId
                });
            }

            farm.FarmDocuments.Add(new FarmDocument
            {
                FarmDocumentId = Guid.NewGuid(),
                FarmId = farm.FarmId,
                FileName = $"{WedgeMarker} commercial-register-{i:D2}.pdf",
                FileUrl = $"https://res.cloudinary.com/demo/raw/upload/wedge/{farm.FarmId:N}/cr.pdf",
                FileSize = 120_000,
                FileType = "application/pdf",
                PublicId = $"wedge/{farm.FarmId:N}/cr",
                UploadedAt = DateTime.UtcNow.AddDays(-20),
                KybKind = KybKind.CommercialRegister
            });
            farm.FarmDocuments.Add(new FarmDocument
            {
                FarmDocumentId = Guid.NewGuid(),
                FarmId = farm.FarmId,
                FileName = $"{WedgeMarker} tax-card-{i:D2}.pdf",
                FileUrl = $"https://res.cloudinary.com/demo/raw/upload/wedge/{farm.FarmId:N}/tax.pdf",
                FileSize = 80_000,
                FileType = "application/pdf",
                PublicId = $"wedge/{farm.FarmId:N}/tax",
                UploadedAt = DateTime.UtcNow.AddDays(-19),
                KybKind = KybKind.TaxCard
            });
            farm.FarmDocuments.Add(new FarmDocument
            {
                FarmDocumentId = Guid.NewGuid(),
                FarmId = farm.FarmId,
                FileName = $"{WedgeMarker} national-id-{i:D2}.pdf",
                FileUrl = $"https://res.cloudinary.com/demo/raw/upload/wedge/{farm.FarmId:N}/nid.pdf",
                FileSize = 60_000,
                FileType = "application/pdf",
                PublicId = $"wedge/{farm.FarmId:N}/nid",
                UploadedAt = DateTime.UtcNow.AddDays(-18),
                KybKind = KybKind.NationalId
            });
            farm.FarmDocuments.Add(new FarmDocument
            {
                FarmDocumentId = Guid.NewGuid(),
                FarmId = farm.FarmId,
                FileName = $"{WedgeMarker} land-lease-{i:D2}.pdf",
                FileUrl = $"https://res.cloudinary.com/demo/raw/upload/wedge/{farm.FarmId:N}/land.pdf",
                FileSize = 90_000,
                FileType = "application/pdf",
                PublicId = $"wedge/{farm.FarmId:N}/land",
                UploadedAt = DateTime.UtcNow.AddDays(-17),
                KybKind = KybKind.LandLease
            });

            db.Farm.Add(farm);
        }

        await db.SaveChangesAsync();

        for (var i = 1; i <= WedgeFactoryCount; i++)
        {
            var email = WedgeFactoryEmail(i);
            var exists = await db.Factory.AnyAsync(f => f.User.Email == email);
            if (exists)
                continue;

            var spec = WedgeFactories[i - 1];
            var user = await EnsureUserAsync(
                userManager,
                email,
                AppRoles.Factory,
                $"01560{i:D6}",
                isVerified: true,
                isActive: true,
                emailConfirmed: true,
                createdDaysAgo: 35 + i,
                fixedUserId: WedgeIds.FactoryUser(i),
                password: WedgePassword);

            db.Factory.Add(new Factory
            {
                FactoryId = WedgeIds.Factory(i),
                UserId = user.Id,
                Name = spec.Name,
                Location = spec.Location,
                Governorate = spec.Governorate,
                Latitude = spec.Lat,
                Longitude = spec.Lon,
                IndustryType = $"{spec.Industry} | Preferred: Tomato | {WedgeMarker} demo density only",
                IsVerified = true,
                AverageRating = 0m,
                RatingCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-(20 + i))
            });
        }

        await db.SaveChangesAsync();

        async Task SeedWedgeRequestAsync(
            Guid requestId,
            Guid factoryId,
            decimal qty,
            decimal price,
            string qualitySpecs)
        {
            if (await db.SupplyRequests.AnyAsync(r => r.RequestId == requestId))
                return;

            db.SupplyRequests.Add(new SupplyRequest
            {
                RequestId = requestId,
                FactoryId = factoryId,
                CropTypeId = tomato.CropTypeId,
                QuantityTons = qty,
                PricePerTon = price,
                DeliveryDate = DateTime.UtcNow.Date.AddDays(45),
                QualitySpecs = qualitySpecs,
                Status = SupplyRequestStatus.Pending,
                DeliveryPoint = DeliveryPoint.FactoryGate,
                FreightPayer = DealParty.Farm,
                TransitRisk = DealParty.Farm,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });
        }

        await SeedWedgeRequestAsync(
            WedgeIds.ExactRequest,
            WedgeIds.Factory(1),
            qty: 40m,
            price: 8000m,
            qualitySpecs:
                $"{WedgeMarker}:REQ-EXACT | Gov:Qalyubia | GeoScope:Exact | Processing grade paste");

        await SeedWedgeRequestAsync(
            WedgeIds.NearbyRequest,
            WedgeIds.Factory(2),
            qty: 35m,
            price: 7900m,
            qualitySpecs:
                $"{WedgeMarker}:REQ-NEAR | Gov:Qalyubia | GeoScope:Nearby | Canning tomatoes");

        await db.SaveChangesAsync();
    }
}
