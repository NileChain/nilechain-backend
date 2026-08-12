using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

public static partial class DevelopmentDataSeeder
{
    private static async Task<List<ApplicationUser>> SeedAdminsAsync(
        UserManager<ApplicationUser> userManager)
    {
        var admins = new List<ApplicationUser>();

        var super = await EnsureUserAsync(
            userManager,
            SuperAdminEmail,
            AppRoles.SuperAdmin,
            "01090000001",
            isVerified: true,
            isActive: true,
            emailConfirmed: true,
            createdDaysAgo: 200);
        admins.Add(super);

        for (var i = 0; i < AdminEmails.Length; i++)
        {
            // Mix: one inactive, one unconfirmed email among admins
            var inactive = i == 2;
            var unconfirmed = i == 1;
            var admin = await EnsureUserAsync(
                userManager,
                AdminEmails[i],
                AppRoles.Admin,
                $"010900000{i + 2:D2}",
                isVerified: true,
                isActive: !inactive,
                emailConfirmed: !unconfirmed,
                createdDaysAgo: 150 - i * 10);
            admins.Add(admin);
        }

        return admins;
    }

    private static async Task<List<Factory>> SeedFactoriesAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        Random rng)
    {
        var factories = new List<Factory>();

        for (var i = 1; i <= SeedFactoryCount; i++)
        {
            var email = FactoryEmail(i);
            var existing = await db.Factory
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.User.Email == email);

            if (existing is not null)
            {
                factories.Add(existing);
                continue;
            }

            // Mix verified / unverified, active / inactive, email confirmed (~70% verified)
            var verified = i % 10 is not (3 or 7 or 9);
            var active = i != 19;
            var emailConfirmed = i != 18;
            var gov = Governorates[(i * 3) % Governorates.Length];
            var industry = FactoryIndustries[(i - 1) % FactoryIndustries.Length];
            var capacity = 200 + i * 45;
            var first = EgyptianFirstNames[rng.Next(EgyptianFirstNames.Length)];
            var last = EgyptianLastNames[rng.Next(EgyptianLastNames.Length)];
            // Prefer Arabic display names for half the factories.
            var name = i % 2 == 0
                ? $"{ArabicFactoryStems[(i - 1) % ArabicFactoryStems.Length]} ({first} {last})"
                : $"{string.Format(
                    FactoryNameTemplates[(i - 1) % FactoryNameTemplates.Length],
                    industry.Split('&')[0].Trim().Split(' ')[0])} ({first} {last})";

            var user = await EnsureUserAsync(
                userManager,
                email,
                AppRoles.Factory,
                $"0101{i:D7}",
                isVerified: verified,
                isActive: active,
                emailConfirmed: emailConfirmed,
                createdDaysAgo: 80 + i);

            // Encode owner name in phone display via realistic naming in factory name
            var factory = new Factory
            {
                FactoryId = Guid.NewGuid(),
                UserId = user.Id,
                Name = name,
                Location = FactoryLocations[(i - 1) % FactoryLocations.Length],
                Governorate = gov,
                IndustryType =
                    $"{industry} | Capacity ~{capacity} tons/month | Preferred: {PickCropsLabel(i)}",
                IsVerified = verified,
                AverageRating = 0m,
                RatingCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-(60 + i * 3))
            };

            db.Factory.Add(factory);
            factories.Add(factory);
        }

        await db.SaveChangesAsync();

        // Reload with users for downstream
        var emails = AllSeedFactoryEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await db.Factory
            .Include(f => f.User)
            .Where(f => f.User.Email != null && emails.Contains(f.User.Email))
            .OrderBy(f => f.User.Email)
            .ToListAsync();
    }

    private static string PickCropsLabel(int index)
    {
        var picks = CropNames
            .Skip((index - 1) % CropNames.Length)
            .Take(3)
            .ToArray();
        if (picks.Length < 3)
            picks = CropNames.Take(3).ToArray();
        return string.Join(", ", picks);
    }

    private static async Task<List<Farm>> SeedFarmsAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<CropType> cropTypes,
        List<Certification> certifications,
        Random rng)
    {
        for (var i = 1; i <= SeedFarmCount; i++)
        {
            var email = FarmEmail(i);
            var exists = await db.Farm.AnyAsync(f => f.User.Email == email);
            if (exists)
                continue;

            // Edge cases by index:
            // 1: excellent rating target (certs + crops)
            // 2: no certifications
            // 3: no crops
            // 4: expired certifications only
            // 5: poor risk / unverified
            // 49: inactive user
            // 50: unconfirmed email
            var noCerts = i is 2 or 7 or 15 or 33;
            var noCrops = i == 3;
            var expiredCerts = i is 4 or 12 or 28;
            // ~70% verified
            var verified = i is not (5 or 11 or 18 or 22 or 31 or 37 or 42 or 46 or 48);
            var active = i != 49;
            var emailConfirmed = i != 50;
            var profileComplete = i is not (3 or 5 or 33);

            // Realistic feddan range 20–500 (keep a few small/large edge cases)
            var size = i switch
            {
                1 => 220m,
                3 => 18m,
                5 => 28m,
                _ => 20m + (i * 9.3m) % 480m
            };

            var risk = i switch
            {
                1 => 92m,
                5 => 18m,
                11 => 28m,
                18 => 35m,
                _ => 40m + (i * 7) % 55m
            };

            var gov = Governorates[(i * 5 + 2) % Governorates.Length];
            var first = EgyptianFirstNames[(i * 3) % EgyptianFirstNames.Length];
            var last = EgyptianLastNames[(i * 5) % EgyptianLastNames.Length];
            // Prefer Arabic مزرعة … names for most farms (demo-friendly).
            var farmName = i % 3 == 0
                ? $"{FarmNamePrefixes[(i - 1) % FarmNamePrefixes.Length]} Farm — {first} {last}"
                : $"مزرعة {ArabicFarmStems[(i - 1) % ArabicFarmStems.Length]} — {first} {last}";

            var user = await EnsureUserAsync(
                userManager,
                email,
                AppRoles.Farm,
                $"0102{i:D7}",
                isVerified: verified,
                isActive: active,
                emailConfirmed: emailConfirmed,
                createdDaysAgo: 100 + i);

            var farm = new Farm
            {
                FarmId = Guid.NewGuid(),
                UserId = user.Id,
                Name = farmName,
                Location = FarmLocations[(i - 1) % FarmLocations.Length],
                Governorate = gov,
                SizeInFeddans = Math.Round(size, 1),
                SoilType = SoilTypes[i % SoilTypes.Length],
                RiskScore = Math.Round(risk, 1),
                IsVerified = verified,
                ProfileComplete = profileComplete,
                AverageRating = 0m,
                RatingCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-(90 + i * 2))
            };

            if (!noCrops)
            {
                var cropCount = 1 + (i % 4);
                for (var c = 0; c < cropCount; c++)
                {
                    var cropName = CropNames[(i + c) % CropNames.Length];
                    var crop = cropTypes.First(ct =>
                        ct.Name.Equals(cropName, StringComparison.OrdinalIgnoreCase));
                    if (farm.FarmCrops.Any(x => x.CropTypeId == crop.CropTypeId))
                        continue;

                    farm.FarmCrops.Add(new FarmCrop
                    {
                        FarmId = farm.FarmId,
                        CropTypeId = crop.CropTypeId,
                        AvailableQuantityTons = 20m + (i % 10) * 5m,
                        AvailableFrom = DateTime.UtcNow.Date.AddMonths(-1),
                        AvailableTo = DateTime.UtcNow.Date.AddMonths(8),
                        MinPricePerTon = 7000m + (i % 5) * 500m,
                        CropType = crop
                    });
                }
            }

            if (!noCerts)
            {
                var certCount = expiredCerts ? 1 + (i % 2) : 1 + (i % 3);
                for (var c = 0; c < certCount; c++)
                {
                    var certName = CertificationNames[(i + c) % CertificationNames.Length];
                    var cert = certifications.First(ct =>
                        ct.Name.Equals(certName, StringComparison.OrdinalIgnoreCase));
                    if (farm.FarmCertifications.Any(fc => fc.CertificationId == cert.CertificationId))
                        continue;

                    if (expiredCerts)
                    {
                        farm.FarmCertifications.Add(new FarmCertification
                        {
                            FarmId = farm.FarmId,
                            CertificationId = cert.CertificationId,
                            IssuedAt = DateTime.UtcNow.AddYears(-2),
                            ExpiresAt = DateTime.UtcNow.AddMonths(-2 - c)
                        });
                    }
                    else
                    {
                        farm.FarmCertifications.Add(new FarmCertification
                        {
                            FarmId = farm.FarmId,
                            CertificationId = cert.CertificationId,
                            IssuedAt = DateTime.UtcNow.AddMonths(-6 - c),
                            ExpiresAt = DateTime.UtcNow.AddMonths(6 + c * 3)
                        });
                    }
                }
            }

            db.Farm.Add(farm);
        }

        await db.SaveChangesAsync();

        var emails = AllSeedFarmEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await db.Farm
            .Include(f => f.User)
            .Include(f => f.FarmCrops)
            .Include(f => f.FarmCertifications)
            .Where(f => f.User.Email != null && emails.Contains(f.User.Email))
            .OrderBy(f => f.User.Email)
            .ToListAsync();
    }

    private static async Task SeedFarmDocumentsAsync(
        NileChainDbContext db,
        List<Farm> farms,
        Random rng)
    {
        // Batch existence check for farm documents
        var farmIds = farms.Select(f => f.FarmId).ToList();
        var existingNames = await db.FarmDocuments
            .Where(d => farmIds.Contains(d.FarmId) && d.FileName.Contains(SeedMarker))
            .Select(d => new { d.FarmId, d.FileName })
            .ToListAsync();
        var existingSet = existingNames
            .Select(d => (d.FarmId, d.FileName))
            .ToHashSet();

        var added = false;

        foreach (var farm in farms)
        {
            var docs = new List<(string Name, string Type, int Size)>
            {
                ($"{SeedMarker} land-title-{farm.FarmId:N}.pdf", "application/pdf", 245_760)
            };

            if (farm.IsVerified)
            {
                docs.Add(($"{SeedMarker} organic-cert-{farm.FarmId:N}.pdf", "application/pdf", 128_000));
                docs.Add(($"{SeedMarker} water-permit-{farm.FarmId:N}.pdf", "application/pdf", 96_000));
            }

            if (farm.FarmCertifications.Count > 0)
                docs.Add(($"{SeedMarker} cert-pack-{farm.FarmId:N}.pdf", "application/pdf", 180_000));

            // Some farms get extra docs for pagination stress
            if (rng.Next(100) < 30)
                docs.Add(($"{SeedMarker} soil-report-{farm.FarmId:N}.pdf", "application/pdf", 210_000));

            foreach (var (fileName, fileType, size) in docs)
            {
                if (existingSet.Contains((farm.FarmId, fileName)))
                    continue;

                db.FarmDocuments.Add(new FarmDocument
                {
                    FarmDocumentId = Guid.NewGuid(),
                    FarmId = farm.FarmId,
                    FileName = fileName,
                    FileUrl = $"https://res.cloudinary.com/demo/raw/upload/seed/{farm.FarmId}/{fileName}",
                    FileSize = size,
                    FileType = fileType,
                    PublicId = $"seed/{farm.FarmId}/{Path.GetFileNameWithoutExtension(fileName)}",
                    UploadedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 90))
                });
                added = true;
            }
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task<int> SeedRagDocumentsAsync(
        NileChainDbContext db,
        List<ApplicationUser> admins)
    {
        var uploader = admins.FirstOrDefault()?.Id
                       ?? throw new InvalidOperationException("No admin available to upload RAG docs.");

        var existingTitles = await db.RagDocuments
            .Where(d => d.Title.Contains(SeedMarker))
            .Select(d => d.Title)
            .ToListAsync();
        var titleSet = existingTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        for (var i = 0; i < KnowledgeDocuments.Length; i++)
        {
            var (category, title, _) = KnowledgeDocuments[i];
            var seedTitle = $"{SeedMarker} {title}";
            if (titleSet.Contains(seedTitle))
                continue;

            db.RagDocuments.Add(new RagDocument
            {
                DocumentId = Guid.NewGuid(),
                Title = seedTitle,
                Category = category,
                FilePath = $"seed/knowledge/{category.Replace(' ', '-').ToLowerInvariant()}/{i:D3}.md",
                UploadedBy = uploader,
                UploadedAt = DateTime.UtcNow.AddDays(-(i + 1))
            });
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync();

        return await db.RagDocuments.CountAsync(d => d.Title.Contains(SeedMarker));
    }
}
