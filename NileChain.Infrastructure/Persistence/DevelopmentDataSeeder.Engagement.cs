using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

public static partial class DevelopmentDataSeeder
{
    private static async Task SeedMessagesAsync(
        NileChainDbContext db,
        List<FarmMatch> matches,
        List<Farm> farms,
        List<Factory> factories,
        Random rng)
    {
        var farmById = farms.ToDictionary(f => f.FarmId);
        var factoryById = factories.ToDictionary(f => f.FactoryId);

        var requestFactoryIds = await db.SupplyRequests
            .AsNoTracking()
            .Where(r => r.QualitySpecs != null && r.QualitySpecs.Contains(SeedMarker))
            .ToDictionaryAsync(r => r.RequestId, r => r.FactoryId);

        var existingKeys = await db.Messages
            .Where(m => m.Content.Contains(SeedMarker))
            .Select(m => m.Content)
            .ToListAsync();
        var contentSet = existingKeys.ToHashSet(StringComparer.Ordinal);

        // Edge: leave some Accepted matches with zero messages (first 5 silent)
        var silentIds = matches
            .Where(m => m.Status == FarmMatchStatus.Accepted)
            .Take(5)
            .Select(m => m.MatchId)
            .ToHashSet();

        var conversational = matches
            .Where(m => m.Status is FarmMatchStatus.Accepted or FarmMatchStatus.Proposed)
            .Where(m => !silentIds.Contains(m.MatchId))
            .ToList();

        // Heavy threads on first 15 matches for "match with many messages"
        var heavyIds = conversational.Take(15).Select(m => m.MatchId).ToHashSet();

        var added = false;
        var created = 0;
        const int targetMessages = 520;

        foreach (var match in conversational)
        {
            if (!farmById.TryGetValue(match.FarmId, out var farm))
                continue;
            if (!requestFactoryIds.TryGetValue(match.RequestId, out var factoryId))
                continue;
            if (!factoryById.TryGetValue(factoryId, out var factory))
                continue;

            var threadLen = heavyIds.Contains(match.MatchId)
                ? 16 + rng.Next(0, 8)
                : 4 + rng.Next(0, 5);

            for (var i = 0; i < threadLen; i++)
            {
                if (contentSet.Count + created >= targetMessages && created > 0
                    && !heavyIds.Contains(match.MatchId))
                    break;

                var fromFactory = i % 2 == 0;
                var template = fromFactory
                    ? MessageTemplatesFactory[i % MessageTemplatesFactory.Length]
                    : MessageTemplatesFarm[i % MessageTemplatesFarm.Length];

                var content = $"{SeedMarker}:MSG-{match.MatchId:N}-{i:D2} {template}";
                if (!contentSet.Add(content))
                    continue;

                db.Messages.Add(new Message
                {
                    MessageId = Guid.NewGuid(),
                    MatchId = match.MatchId,
                    SenderId = fromFactory ? factory.UserId : farm.UserId,
                    ReceiverId = fromFactory ? farm.UserId : factory.UserId,
                    Content = content,
                    IsRead = i < threadLen - 2 || rng.Next(100) < 60,
                    CreatedAt = DateTime.UtcNow
                        .AddDays(-(threadLen - i))
                        .AddHours(i)
                        .AddMinutes(rng.Next(0, 50))
                });
                created++;
                added = true;
            }

            if (created % 100 == 0 && added)
            {
                await db.SaveChangesAsync();
                added = false;
            }
        }

        // Top-up short threads on remaining matches if under target
        var idx = 0;
        while (contentSet.Count < targetMessages && conversational.Count > 0)
        {
            var match = conversational[idx % conversational.Count];
            idx++;
            if (!farmById.TryGetValue(match.FarmId, out var farm))
                continue;
            if (!requestFactoryIds.TryGetValue(match.RequestId, out var factoryId))
                continue;
            if (!factoryById.TryGetValue(factoryId, out var factory))
                continue;

            var n = contentSet.Count;
            var content =
                $"{SeedMarker}:MSG-{match.MatchId:N}-X{n:D4} Follow-up on delivery logistics and packing specs.";
            if (!contentSet.Add(content))
                continue;

            var fromFactory = n % 2 == 0;
            db.Messages.Add(new Message
            {
                MessageId = Guid.NewGuid(),
                MatchId = match.MatchId,
                SenderId = fromFactory ? factory.UserId : farm.UserId,
                ReceiverId = fromFactory ? farm.UserId : factory.UserId,
                Content = content,
                IsRead = rng.Next(100) < 50,
                CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 500))
            });
            added = true;

            if (n > 2000)
                break;
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task SeedReviewsAsync(
        NileChainDbContext db,
        List<Contract> contracts,
        List<FarmMatch> matches,
        List<Farm> farms,
        List<Factory> factories,
        Random rng)
    {
        var signed = contracts.Where(c => c.Status == ContractStatus.Signed).ToList();
        if (signed.Count == 0)
            return;

        var matchById = matches.ToDictionary(m => m.MatchId);
        var farmById = farms.ToDictionary(f => f.FarmId);
        var requestFactory = await db.SupplyRequests.ToDictionaryAsync(r => r.RequestId, r => r.FactoryId);
        var factoryById = factories.ToDictionary(f => f.FactoryId);

        var existing = await db.Reviews
            .Where(r => r.Comment != null && r.Comment.Contains(SeedMarker))
            .Select(r => new { r.ContractId, r.ReviewerId })
            .ToListAsync();
        var pairSet = existing.Select(e => (e.ContractId, e.ReviewerId)).ToHashSet();

        var added = false;
        var created = 0;
        const int targetReviews = 150;

        // Mix ratings 1–5 with bias; force some 1s and 5s for edge cases
        int NextRating(int i) => i switch
        {
            0 => 5,
            1 => 5,
            2 => 1,
            3 => 2,
            _ => 1 + (i * 3 + rng.Next(0, 2)) % 5
        };

        for (var i = 0; i < signed.Count && created < targetReviews; i++)
        {
            var contract = signed[i];
            if (!matchById.TryGetValue(contract.MatchId, out var match))
                continue;
            if (!farmById.TryGetValue(match.FarmId, out var farm))
                continue;
            if (!requestFactory.TryGetValue(match.RequestId, out var factoryId))
                continue;
            if (!factoryById.TryGetValue(factoryId, out var factory))
                continue;

            // Factory → Farm
            if (pairSet.Add((contract.ContractId, factory.UserId)))
            {
                db.Reviews.Add(new Review
                {
                    ReviewId = Guid.NewGuid(),
                    ContractId = contract.ContractId,
                    ReviewerId = factory.UserId,
                    TargetId = farm.UserId,
                    Rating = NextRating(created),
                    Comment = $"{SeedMarker} {ReviewComments[created % ReviewComments.Length]}",
                    CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 40))
                });
                created++;
                added = true;
            }

            if (created >= targetReviews)
                break;

            // Farm → Factory (reciprocal)
            if (pairSet.Add((contract.ContractId, farm.UserId)))
            {
                db.Reviews.Add(new Review
                {
                    ReviewId = Guid.NewGuid(),
                    ContractId = contract.ContractId,
                    ReviewerId = farm.UserId,
                    TargetId = factory.UserId,
                    Rating = NextRating(created + 3),
                    Comment =
                        $"{SeedMarker} {ReviewComments[(created + 5) % ReviewComments.Length]}",
                    CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 35))
                });
                created++;
                added = true;
            }
        }

        if (added)
            await db.SaveChangesAsync();
    }

    private static async Task RecalculateRatingsAsync(NileChainDbContext db)
    {
        var reviews = await db.Reviews.AsNoTracking().ToListAsync();
        var farmEmails = AllSeedFarmEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var factoryEmails = AllSeedFactoryEmails().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var farms = await db.Farm
            .Include(f => f.User)
            .Where(f => f.User.Email != null && farmEmails.Contains(f.User.Email))
            .ToListAsync();
        var factories = await db.Factory
            .Include(f => f.User)
            .Where(f => f.User.Email != null && factoryEmails.Contains(f.User.Email))
            .ToListAsync();

        foreach (var farm in farms)
        {
            var farmReviews = reviews.Where(r => r.TargetId == farm.UserId).ToList();
            if (farmReviews.Count == 0)
            {
                // Seed display ratings for farms without reviews so UI isn't empty
                if (farm.AverageRating == 0 && farm.User.Email == FarmEmail(1))
                {
                    farm.AverageRating = 4.9m;
                    farm.RatingCount = 12;
                }
                else if (farm.AverageRating == 0 && farm.User.Email == FarmEmail(5))
                {
                    farm.AverageRating = 1.5m;
                    farm.RatingCount = 4;
                }

                continue;
            }

            farm.RatingCount = farmReviews.Count;
            farm.AverageRating = Math.Round((decimal)farmReviews.Average(r => r.Rating), 2);
        }

        foreach (var factory in factories)
        {
            var factoryReviews = reviews.Where(r => r.TargetId == factory.UserId).ToList();
            if (factoryReviews.Count == 0)
                continue;

            factory.RatingCount = factoryReviews.Count;
            factory.AverageRating = Math.Round((decimal)factoryReviews.Average(r => r.Rating), 2);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedNotificationsAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        List<Farm> farms,
        List<Factory> factories,
        List<ApplicationUser> admins,
        Random rng)
    {
        var existing = await db.Notifications
            .Where(n => n.Title.Contains(SeedMarker))
            .Select(n => new { n.UserId, n.Title })
            .ToListAsync();
        var pairSet = existing.Select(e => (e.UserId, e.Title)).ToHashSet();

        var templates = new (string Title, string Message, string Type)[]
        {
            ("New match proposed", "A factory proposed a match for your farm.", "Match"),
            ("Match accepted", "A farm accepted your supply match proposal.", "Match"),
            ("Contract ready", "A supply contract draft is ready for review.", "Contract"),
            ("Contract signed", "A party signed the supply contract.", "Contract"),
            ("New message", "You have a new message in an active match thread.", "Message"),
            ("Profile tip", "Upload remaining documents to improve match score.", "System"),
            ("Verification update", "Your account verification status was updated.", "Admin"),
            ("Review received", "You received a new rating after a completed contract.", "Review"),
            ("Market alert", "Crop prices shifted in your governorate this week.", "System"),
            ("Delivery reminder", "An upcoming delivery window starts within 7 days.", "Contract")
        };

        var added = false;
        var created = 0;
        const int target = 300;
        var existingCount = existing.Count;
        if (existingCount >= target)
            return;

        void TryAdd(Guid userId, string title, string message, string type, bool isRead, int hoursAgo)
        {
            if (existingCount + created >= target)
                return;
            var seedTitle = $"{SeedMarker} {title} #{existingCount + created + 1:D3}";
            if (!pairSet.Add((userId, seedTitle)))
                return;

            db.Notifications.Add(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = userId,
                Title = seedTitle,
                Message = message,
                Type = type,
                IsRead = isRead,
                CreatedAt = DateTime.UtcNow.AddHours(-hoursAgo)
            });
            created++;
            added = true;
        }

        var users = farms.Select(f => f.UserId)
            .Concat(factories.Select(f => f.UserId))
            .Concat(admins.Select(a => a.Id))
            .Distinct()
            .ToList();

        var legacyAdmin = await userManager.FindByEmailAsync("admin@gmail.com");
        if (legacyAdmin is not null)
            users.Add(legacyAdmin.Id);

        var u = 0;
        while (existingCount + created < target && users.Count > 0)
        {
            var userId = users[u % users.Count];
            var t = templates[(existingCount + created) % templates.Length];
            var type = NotificationTypes[(existingCount + created) % NotificationTypes.Length];
            TryAdd(
                userId,
                t.Title,
                t.Message,
                string.IsNullOrEmpty(type) ? t.Type : type,
                isRead: (existingCount + created) % 3 != 0,
                hoursAgo: rng.Next(1, 720));
            u++;

            if (created % 80 == 0 && added)
            {
                await db.SaveChangesAsync();
                added = false;
            }

            // Safety
            if (u > target * 5)
                break;
        }

        if (added)
            await db.SaveChangesAsync();
    }
}
