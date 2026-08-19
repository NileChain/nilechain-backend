using NileChain.AI.Matching;
using NileChain.Domain.Common;

namespace NileChain.AI.Evaluation;

/// <summary>
/// The behaviours the agent is not allowed to lose: geographic scope is never widened behind the
/// factory's back, shortlist counts add up, the trail names the tools that actually ran, and a
/// low-trust top farm raises a warning before any contract talk.
/// </summary>
public static class GoldenSet
{
    public const string SearchFarms = "SearchFarms";
    public const string CalculateRiskScore = "CalculateRiskScore";
    public const string WidenSearchRadius = "WidenSearchRadius";
    public const string FlagLowRiskWarning = "FlagLowRiskWarning";
    public const string GenerateContract = "GenerateContract";

    private const string Giza = "Giza";
    private const string Qalyubia = "Qalyubia";
    private const string Aswan = "Aswan";
    private const string Cairo = "Cairo";

    public static IReadOnlyList<GoldenScenario> All { get; } =
    [
        new GoldenScenario(
            "exact-stays-exact",
            "An Exact request never reaches a neighbouring or distant governorate.",
            new GoldenWorld(
                Crop: "Tomato",
                FactoryGovernorate: Giza,
                GeoScope: "Exact",
                Farms:
                [
                    new GoldenFarm("Giza Unverified", Giza, FarmTrustBand.Medium, Verified: false),
                    new GoldenFarm("Qalyubia Verified", Qalyubia, FarmTrustBand.High, Verified: true),
                    new GoldenFarm("Aswan Verified", Aswan, FarmTrustBand.High, Verified: true)
                ]),
            new GoldenExpectation(
                TopMatchCount: 1,
                TotalEligible: 1,
                TruncatedCount: 0,
                AllowedGovernorates: [Giza],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore],
                ForbiddenTrailFunctions: [WidenSearchRadius, GenerateContract],
                // The better neighbour is offered as a hint, not slipped into the shortlist.
                PeekGovernorate: Qalyubia)),

        new GoldenScenario(
            "exact-opt-in-adds-one-ring-only",
            "With the factory's one-ring opt-in, the neighbour joins but the far governorate still does not.",
            new GoldenWorld(
                Crop: "Beans",
                FactoryGovernorate: Giza,
                GeoScope: "Exact",
                Farms:
                [
                    new GoldenFarm("Giza Farm", Giza, FarmTrustBand.High, Verified: true),
                    new GoldenFarm("Qalyubia Farm", Qalyubia, FarmTrustBand.High, Verified: true),
                    new GoldenFarm("Aswan Farm", Aswan, FarmTrustBand.High, Verified: true)
                ],
                FactoryApprovedOneRingExpansion: true),
            new GoldenExpectation(
                TopMatchCount: 2,
                TotalEligible: 2,
                TruncatedCount: 0,
                AllowedGovernorates: [Giza, Qalyubia],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore],
                ForbiddenTrailFunctions: [WidenSearchRadius])),

        new GoldenScenario(
            "nearby-never-becomes-nationwide",
            "Nearby keeps to its adjacency ring; a distant high-trust farm is not worth breaking scope for.",
            new GoldenWorld(
                Crop: "Wheat",
                FactoryGovernorate: Giza,
                GeoScope: "Nearby",
                Farms:
                [
                    new GoldenFarm("Giza Farm", Giza, FarmTrustBand.Medium, Verified: true),
                    new GoldenFarm("Qalyubia Farm", Qalyubia, FarmTrustBand.High, Verified: true),
                    new GoldenFarm("Aswan Farm", Aswan, FarmTrustBand.High, Verified: true)
                ]),
            new GoldenExpectation(
                TopMatchCount: 2,
                TotalEligible: 2,
                TruncatedCount: 0,
                AllowedGovernorates: [Giza, Qalyubia],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore],
                ForbiddenTrailFunctions: [WidenSearchRadius])),

        new GoldenScenario(
            "nationwide-reaches-everywhere-without-a-peek",
            "Nationwide already includes the whole country, so there is nothing left to hint at.",
            new GoldenWorld(
                Crop: "Rice",
                FactoryGovernorate: Giza,
                GeoScope: "Nationwide",
                Farms:
                [
                    new GoldenFarm("Giza Farm", Giza, FarmTrustBand.High, Verified: true),
                    new GoldenFarm("Aswan Farm", Aswan, FarmTrustBand.High, Verified: true)
                ]),
            new GoldenExpectation(
                TopMatchCount: 2,
                TotalEligible: 2,
                TruncatedCount: 0,
                AllowedGovernorates: [Giza, Aswan],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore],
                ForbiddenTrailFunctions: [])),

        new GoldenScenario(
            "shortlist-cap-reports-what-it-hid",
            "Eight eligible farms, five shown: the truncated count must account for the rest.",
            new GoldenWorld(
                Crop: "Corn",
                FactoryGovernorate: Giza,
                GeoScope: "Exact",
                Farms: EightGizaFarms()),
            new GoldenExpectation(
                TopMatchCount: MatchingLimits.DefaultMaxResults,
                TotalEligible: 8,
                TruncatedCount: 8 - MatchingLimits.DefaultMaxResults,
                AllowedGovernorates: [Giza],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore],
                ForbiddenTrailFunctions: [WidenSearchRadius])),

        new GoldenScenario(
            "show-more-raises-the-cap-not-the-scope",
            "Asking for more farms shows all eight without touching the governorate filter.",
            new GoldenWorld(
                Crop: "Mango",
                FactoryGovernorate: Giza,
                GeoScope: "Exact",
                Farms:
                [
                    .. EightGizaFarms(),
                    new GoldenFarm("Cairo Extra", Cairo, FarmTrustBand.High, Verified: true)
                ],
                ShortlistTakeLimit: MatchingLimits.MaxShowMoreResults),
            new GoldenExpectation(
                TopMatchCount: 8,
                TotalEligible: 8,
                TruncatedCount: 0,
                AllowedGovernorates: [Giza],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore],
                ForbiddenTrailFunctions: [WidenSearchRadius])),

        new GoldenScenario(
            "no-eligible-farm-fails-loudly",
            "Nothing in scope means an unsuccessful run, not an invented farm.",
            new GoldenWorld(
                Crop: "Barley",
                FactoryGovernorate: Giza,
                GeoScope: "Exact",
                Farms: [new GoldenFarm("Aswan Farm", Aswan, FarmTrustBand.High, Verified: true)]),
            new GoldenExpectation(
                TopMatchCount: 0,
                TotalEligible: 0,
                TruncatedCount: 0,
                AllowedGovernorates: [],
                RequiredTrailFunctions: [SearchFarms],
                ForbiddenTrailFunctions: [CalculateRiskScore, GenerateContract],
                Success: false)),

        new GoldenScenario(
            "low-trust-top-farm-raises-a-warning",
            "A weak best farm must warn before anything contractual happens.",
            new GoldenWorld(
                Crop: "Onion",
                FactoryGovernorate: Giza,
                GeoScope: "Exact",
                Farms: [new GoldenFarm("Giza Weak", Giza, FarmTrustBand.Low, Verified: false)]),
            new GoldenExpectation(
                TopMatchCount: 1,
                TotalEligible: 1,
                TruncatedCount: 0,
                AllowedGovernorates: [Giza],
                RequiredTrailFunctions: [SearchFarms, CalculateRiskScore, FlagLowRiskWarning],
                ForbiddenTrailFunctions: [GenerateContract],
                ExpectRiskWarning: true))
    ];

    private static List<GoldenFarm> EightGizaFarms() =>
        Enumerable.Range(0, 8)
            .Select(i => new GoldenFarm(
                $"Giza {i}",
                Giza,
                i < 4 ? FarmTrustBand.High : FarmTrustBand.Medium,
                Verified: true))
            .ToList();
}
