using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

public static partial class DevelopmentDataSeeder
{
    private static async Task<List<SupplyRequest>> SeedSupplyRequestsAsync(
        NileChainDbContext db,
        List<Factory> factories,
        List<CropType> cropTypes,
        Random rng)
    {
        const int targetCount = SeedSupplyRequestCount;
        // Factory index 9 (10th) intentionally gets zero requests — edge case.
        var activeFactories = factories
            .Where((_, idx) => idx < factories.Count - 1)
            .ToList();
        if (activeFactories.Count == 0)
            throw new InvalidOperationException("No factories available for supply request seed.");

        var existingMarkers = await db.SupplyRequests
            .Where(r => r.QualitySpecs != null && r.QualitySpecs.Contains(SeedMarker))
            .Select(r => r.QualitySpecs!)
            .ToListAsync();

        var existingByKey = existingMarkers
            .Select(spec =>
            {
                var start = spec.IndexOf($"{SeedMarker}:REQ-", StringComparison.Ordinal);
                if (start < 0) return null;
                var keyStart = start + SeedMarker.Length + 1;
                var keyEnd = spec.IndexOf(' ', keyStart);
                if (keyEnd < 0) keyEnd = spec.IndexOf('|', keyStart);
                var key = keyEnd < 0
                    ? spec[keyStart..].Trim()
                    : spec[keyStart..keyEnd].Trim();
                return key;
            })
            .Where(k => k is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingEntities = await db.SupplyRequests
            .Where(r => r.QualitySpecs != null && r.QualitySpecs.Contains(SeedMarker))
            .ToListAsync();

        var statuses = new[]
        {
            SupplyRequestStatus.Pending,
            SupplyRequestStatus.Matched,
            SupplyRequestStatus.Fulfilled,
            SupplyRequestStatus.Cancelled,
            SupplyRequestStatus.Pending,
            SupplyRequestStatus.Matched,
            SupplyRequestStatus.Matched,
            SupplyRequestStatus.Fulfilled
        };

        var results = new List<SupplyRequest>(existingEntities);
        var added = false;

        for (var i = 1; i <= targetCount; i++)
        {
            var key = $"REQ-{i:D3}";
            if (existingByKey.Contains(key))
                continue;

            var marker = $"{SeedMarker}:{key}";
            var factory = activeFactories[(i - 1) % activeFactories.Count];
            var crop = cropTypes.First(c =>
                c.Name.Equals(CropNames[(i - 1) % CropNames.Length], StringComparison.OrdinalIgnoreCase));
            var status = statuses[(i - 1) % statuses.Length];
            if (i is 1 or 2)
                status = SupplyRequestStatus.Pending;

            var gov = Governorates[(i * 2) % Governorates.Length];
            var qty = 10m + (i * 7) % 120;
            var basePrice = BaseCropPrices.GetValueOrDefault(crop.Name, 9000m);
            var price = basePrice + rng.Next(-800, 900);
            var deliveryOffset = status == SupplyRequestStatus.Fulfilled
                ? -rng.Next(5, 40)
                : rng.Next(10, 75);
            var specs = QualitySpecTemplates[(i - 1) % QualitySpecTemplates.Length];

            var request = new SupplyRequest
            {
                RequestId = Guid.NewGuid(),
                FactoryId = factory.FactoryId,
                CropTypeId = crop.CropTypeId,
                QuantityTons = qty,
                QualitySpecs = $"{marker} | Gov:{gov} | {specs}",
                PricePerTon = price,
                DeliveryDate = DateTime.UtcNow.AddDays(deliveryOffset),
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-(Math.Abs(deliveryOffset) + rng.Next(3, 20)))
            };

            db.SupplyRequests.Add(request);
            results.Add(request);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return results;
    }

    private static async Task<List<FarmMatch>> SeedFarmMatchesAsync(
        NileChainDbContext db,
        List<Farm> farms,
        List<SupplyRequest> requests,
        Random rng)
    {
        if (farms.Count == 0 || requests.Count == 0)
            return [];

        var existingPairs = await db.FarmMatches
            .Select(m => new { m.RequestId, m.FarmId })
            .ToListAsync();
        var pairSet = existingPairs
            .Select(p => (p.RequestId, p.FarmId))
            .ToHashSet();

        var existingMatches = await db.FarmMatches
            .Where(m => m.SupplyRequest.QualitySpecs != null
                        && m.SupplyRequest.QualitySpecs.Contains(SeedMarker))
            .ToListAsync();

        // Leave REQ-001..REQ-005 with zero matches (fresh requests for live agent demos)
        var matchable = requests
            .Where(r => r.QualitySpecs != null
                        && !r.QualitySpecs.Contains("REQ-001")
                        && !r.QualitySpecs.Contains("REQ-002")
                        && !r.QualitySpecs.Contains("REQ-003")
                        && !r.QualitySpecs.Contains("REQ-004")
                        && !r.QualitySpecs.Contains("REQ-005"))
            .ToList();

        var statuses = new[]
        {
            FarmMatchStatus.Proposed,
            FarmMatchStatus.Accepted,
            FarmMatchStatus.Rejected,
            FarmMatchStatus.Expired,
            FarmMatchStatus.Proposed,
            FarmMatchStatus.Accepted,
            FarmMatchStatus.Accepted
        };

        var added = false;
        var created = 0;
        const int targetMatches = SeedMatchTarget;

        // Ensure AI score bands: high / medium / poor across farms
        var scoreBands = new (decimal Min, decimal Max)[]
        {
            (85m, 98m), // high
            (55m, 75m), // medium
            (25m, 45m), // poor
            (70m, 90m),
            (40m, 60m)
        };

        var farmIndex = 0;
        foreach (var request in matchable)
        {
            if (existingMatches.Count >= targetMatches)
                break;

            // 3–6 matches per request until we near target
            var perRequest = 3 + (existingMatches.Count % 4);
            var usedFarms = new HashSet<Guid>();

            for (var m = 0; m < perRequest; m++)
            {
                if (existingMatches.Count >= targetMatches)
                    break;

                Farm? farm = null;
                for (var attempt = 0; attempt < farms.Count; attempt++)
                {
                    var candidate = farms[farmIndex % farms.Count];
                    farmIndex++;
                    if (usedFarms.Contains(candidate.FarmId))
                        continue;
                    if (pairSet.Contains((request.RequestId, candidate.FarmId)))
                        continue;
                    farm = candidate;
                    break;
                }

                if (farm is null)
                    break;

                usedFarms.Add(farm.FarmId);
                pairSet.Add((request.RequestId, farm.FarmId));

                var band = scoreBands[(existingMatches.Count + m) % scoreBands.Length];
                var score = Math.Round(
                    band.Min + (decimal)rng.NextDouble() * (band.Max - band.Min), 1);
                var risk = farm.RiskScore ?? Math.Round(20m + (decimal)rng.NextDouble() * 70m, 1);
                var status = statuses[(existingMatches.Count + m) % statuses.Length];

                // Bias Accepted when request already Matched/Fulfilled
                if (request.Status is SupplyRequestStatus.Matched or SupplyRequestStatus.Fulfilled
                    && m == 0)
                    status = FarmMatchStatus.Accepted;

                var match = new FarmMatch
                {
                    MatchId = Guid.NewGuid(),
                    RequestId = request.RequestId,
                    FarmId = farm.FarmId,
                    MatchScore = score,
                    RiskScore = risk,
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddDays(-(rng.Next(1, 45)))
                };

                db.FarmMatches.Add(match);
                existingMatches.Add(match);
                created++;
                added = true;
            }
        }

        // Top up to ~300 if short (bounded attempts)
        var safety = 0;
        while (existingMatches.Count < targetMatches && safety < targetMatches * 20)
        {
            safety++;
            var request = matchable[rng.Next(matchable.Count)];
            var farm = farms[rng.Next(farms.Count)];
            if (!pairSet.Add((request.RequestId, farm.FarmId)))
                continue;

            var band = scoreBands[existingMatches.Count % scoreBands.Length];
            var score = Math.Round(
                band.Min + (decimal)rng.NextDouble() * (band.Max - band.Min), 1);

            var match = new FarmMatch
            {
                MatchId = Guid.NewGuid(),
                RequestId = request.RequestId,
                FarmId = farm.FarmId,
                MatchScore = score,
                RiskScore = farm.RiskScore ?? 50m,
                Status = statuses[existingMatches.Count % statuses.Length],
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 40))
            };

            db.FarmMatches.Add(match);
            existingMatches.Add(match);
            created++;
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return existingMatches;
    }

    private static async Task<List<Contract>> SeedContractsAsync(
        NileChainDbContext db,
        List<FarmMatch> _,
        Random rng)
    {
        const int targetContracts = SeedContractTarget;

        // Work on tracked seed matches so Accepted promotions persist.
        var tracked = await db.FarmMatches
            .Where(m => m.SupplyRequest.QualitySpecs != null
                        && m.SupplyRequest.QualitySpecs.Contains(SeedMarker))
            .ToListAsync();

        var accepted = tracked
            .Where(m => m.Status == FarmMatchStatus.Accepted)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (accepted.Count < targetContracts)
        {
            var needed = targetContracts - accepted.Count;
            var promote = tracked
                .Where(m => m.Status == FarmMatchStatus.Proposed)
                .Take(needed)
                .ToList();

            foreach (var m in promote)
            {
                m.Status = FarmMatchStatus.Accepted;
                accepted.Add(m);
            }

            if (promote.Count > 0)
                await db.SaveChangesAsync();
        }

        // Bias toward Signed so ~150 bidirectional reviews are possible (~75 Signed × 2).
        var statusCycle = new[]
        {
            ContractStatus.Signed,
            ContractStatus.Signed,
            ContractStatus.Signed,
            ContractStatus.Draft,
            ContractStatus.PendingSignature,
            ContractStatus.Signed,
            ContractStatus.Cancelled,
            ContractStatus.Signed,
            ContractStatus.PendingSignature,
            ContractStatus.Signed
        };

        var existingByMatch = await db.Contracts
            .Where(c => c.GeneratedText != null && c.GeneratedText.Contains(SeedMarker))
            .ToDictionaryAsync(c => c.MatchId);

        var contracts = existingByMatch.Values.ToList();
        var added = false;

        foreach (var match in accepted.Take(targetContracts))
        {
            if (existingByMatch.ContainsKey(match.MatchId))
                continue;

            var status = statusCycle[contracts.Count % statusCycle.Length];
            var contract = new Contract
            {
                ContractId = Guid.NewGuid(),
                MatchId = match.MatchId,
                GeneratedText =
                    $"{SeedMarker} عقد توريد زراعي — Development supply contract for match {match.MatchId:N}.\n" +
                    "بسم الله الرحمن الرحيم\n" +
                    "**عقد توريد زراعي**\n" +
                    "الطرف الأول (المشتري / المصنع) والطرف الثاني (المورد / المزرعة) يوافقان على الكمية والمواصفات وموعد التوريد.\n" +
                    $"Status={status}. Governed by Egyptian civil code. Force majeure includes extreme Nile flood events.",
                PdfUrl = status is ContractStatus.Signed or ContractStatus.PendingSignature
                    ? $"https://res.cloudinary.com/demo/raw/upload/seed/contracts/{match.MatchId:N}.pdf"
                    : null,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 50)),
                SignedAt = status == ContractStatus.Signed
                    ? DateTime.UtcNow.AddDays(-rng.Next(1, 30))
                    : null,
                FactorySignedAt = status switch
                {
                    ContractStatus.Signed => DateTime.UtcNow.AddDays(-rng.Next(3, 35)),
                    ContractStatus.PendingFarmSignature => DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
                    _ => null
                },
                FarmSignedAt = status switch
                {
                    ContractStatus.Signed => DateTime.UtcNow.AddDays(-rng.Next(1, 28)),
                    ContractStatus.PendingFactorySignature => DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
                    _ => null
                }
            };

            db.Contracts.Add(contract);
            contracts.Add(contract);
            existingByMatch[match.MatchId] = contract;
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();

        return contracts;
    }
}
