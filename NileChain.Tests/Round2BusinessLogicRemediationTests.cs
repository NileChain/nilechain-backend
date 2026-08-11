using NileChain.AI.Matching;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Validation.Factory;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class Round2BusinessLogicRemediationTests
{
    [Fact]
    public void Matching_RankAndTake_ExposesTruncationMetadata()
    {
        var farms = Enumerable.Range(0, 8)
            .Select(i => new MatchResult
            {
                FarmId = Guid.Parse($"00000000-0000-0000-0000-{i + 1:D12}"),
                FarmName = $"Farm {i}",
                MatchScore = 80,
                RiskScore = 50,
                IsVerified = true
            })
            .ToList();

        var taken = MatchingPlugin.RankAndTake(farms, maxResults: 5);
        Assert.Equal(5, taken.Count);

        var totalEligible = farms.Count;
        var truncated = Math.Max(0, totalEligible - taken.Count);
        Assert.Equal(3, truncated);

        var wrapper = new MatchSearchResult
        {
            Results = taken.ToList(),
            TotalEligible = totalEligible,
            TruncatedCount = truncated,
            TakeLimit = 5
        };
        Assert.Equal(8, wrapper.TotalEligible);
        Assert.Equal(3, wrapper.TruncatedCount);
        Assert.Equal(5, wrapper.Results.Count);
    }

    [Fact]
    public void Matching_TieBreak_IsStableByFarmId()
    {
        var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var c = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var farms = new[]
        {
            new MatchResult { FarmId = c, MatchScore = 90, RiskScore = 50, IsVerified = true },
            new MatchResult { FarmId = a, MatchScore = 90, RiskScore = 50, IsVerified = true },
            new MatchResult { FarmId = b, MatchScore = 90, RiskScore = 50, IsVerified = true },
        };

        var ranked = MatchingPlugin.RankAndTake(farms, maxResults: 5);
        Assert.Equal(new[] { a, b, c }, ranked.Select(r => r.FarmId).ToArray());
    }

    [Fact]
    public void MatchingLimits_DefaultAndConfigured()
    {
        Assert.Equal(5, MatchingLimits.DefaultMaxResults);
        Assert.Equal(5, MatchingLimits.ResolveMaxResults(null));
        Assert.Equal(5, MatchingLimits.ResolveMaxResults(0));
        Assert.Equal(10, MatchingLimits.ResolveMaxResults(10));
    }

    [Fact]
    public void ActiveFarmFilter_ExcludesInactiveOnly()
    {
        Assert.True(ActiveFarmFilter.IsEligible(userIsActive: null));
        Assert.True(ActiveFarmFilter.IsEligible(userIsActive: true));
        Assert.False(ActiveFarmFilter.IsEligible(userIsActive: false));

        var inactive = new HashSet<Guid> { Guid.Parse("11111111-1111-1111-1111-111111111111") };
        Assert.False(ActiveFarmFilter.IsEligibleUserId(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), inactive));
        Assert.True(ActiveFarmFilter.IsEligibleUserId(Guid.NewGuid(), inactive));
    }

    [Fact]
    public void PriceValidator_RejectsZeroAndOverMax()
    {
        var validator = new CreateSupplyRequestRequestValidator();

        var zero = validator.Validate(new CreateSupplyRequestRequest
        {
            Crop = "Wheat",
            Quantity = 10,
            Price = 0,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(7)
        });
        Assert.Contains(zero.Errors, e => e.PropertyName == nameof(CreateSupplyRequestRequest.Price));

        var tooHigh = validator.Validate(new CreateSupplyRequestRequest
        {
            Crop = "Wheat",
            Quantity = 10,
            Price = 100_000_000m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(7)
        });
        Assert.Contains(tooHigh.Errors, e => e.PropertyName == nameof(CreateSupplyRequestRequest.Price));

        var ok = validator.Validate(new CreateSupplyRequestRequest
        {
            Crop = "Wheat",
            Quantity = 10,
            Price = 99_999_999.99m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(7)
        });
        Assert.DoesNotContain(ok.Errors, e => e.PropertyName == nameof(CreateSupplyRequestRequest.Price));
    }

    [Fact]
    public void DeliveryDateValidator_RejectsPast_AllowsToday()
    {
        var validator = new CreateSupplyRequestRequestValidator();
        var today = DateTime.UtcNow.Date;

        var past = validator.Validate(new CreateSupplyRequestRequest
        {
            Crop = "Wheat",
            Quantity = 1,
            Price = 100,
            DeliveryDate = today.AddDays(-1)
        });
        Assert.Contains(past.Errors, e => e.PropertyName == nameof(CreateSupplyRequestRequest.DeliveryDate));

        var todayReq = validator.Validate(new CreateSupplyRequestRequest
        {
            Crop = "Wheat",
            Quantity = 1,
            Price = 100,
            DeliveryDate = today
        });
        Assert.DoesNotContain(
            todayReq.Errors,
            e => e.PropertyName == nameof(CreateSupplyRequestRequest.DeliveryDate));

        Assert.True(DeliveryDatePolicy.IsNotInPast(today));
        Assert.False(DeliveryDatePolicy.IsNotInPast(today.AddDays(-1)));

        var stored = DeliveryDatePolicy.ToUtcStorage(today);
        Assert.Equal(12, stored.Hour);
        Assert.Equal(DateTimeKind.Utc, stored.Kind);
    }

    [Fact]
    public void ContractMatchExpiry_SelectsAgedProposedAndPending()
    {
        var now = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        var matches = new[]
        {
            (Status: FarmMatchStatus.Proposed, Created: now.AddDays(-15)),
            (Status: FarmMatchStatus.Proposed, Created: now.AddDays(-1)),
            (Status: FarmMatchStatus.Accepted, Created: now.AddDays(-30)),
            (Status: FarmMatchStatus.Expired, Created: now.AddDays(-30)),
        };

        var expired = ContractMatchExpiry.SelectExpiredProposedMatches(
            matches,
            m => m.Status,
            m => m.Created,
            now,
            expiryDays: 14);

        Assert.Single(expired);
        Assert.Equal(FarmMatchStatus.Proposed, expired[0].Status);

        var contracts = new[]
        {
            (Status: ContractStatus.PendingSignature, Created: now.AddDays(-20)),
            (Status: ContractStatus.Signed, Created: now.AddDays(-40)),
            (Status: ContractStatus.PendingFarmSignature, Created: now.AddDays(-2)),
        };

        var cancelled = ContractMatchExpiry.SelectExpiredPendingContracts(
            contracts,
            c => c.Status,
            c => c.Created,
            now,
            expiryDays: 14);

        Assert.Single(cancelled);
        Assert.Equal(ContractStatus.PendingSignature, cancelled[0].Status);
    }

    [Fact]
    public void MatchGovernoratePolicy_RejectsExactMismatch()
    {
        Assert.True(MatchGovernoratePolicy.IsExactScope("Gov:Giza | GeoScope:Exact"));
        Assert.False(MatchGovernoratePolicy.IsExactScope("Gov:Giza | GeoScope:Nearby"));

        Assert.True(MatchGovernoratePolicy.IsSnapshotStillValid(
            "Giza",
            "Giza",
            "Gov:Giza | GeoScope:Exact"));

        Assert.False(MatchGovernoratePolicy.IsSnapshotStillValid(
            "Giza",
            "Cairo",
            "Gov:Giza | GeoScope:Exact"));

        Assert.True(MatchGovernoratePolicy.IsSnapshotStillValid(
            "Giza",
            "Cairo",
            "Gov:Giza | GeoScope:Nationwide"));

        Assert.True(MatchGovernoratePolicy.IsSnapshotStillValid(
            null,
            "Cairo",
            "Gov:Giza | GeoScope:Exact"));
    }
}
