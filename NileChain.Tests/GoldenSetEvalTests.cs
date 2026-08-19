using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.AI;
using NileChain.AI.Agents;
using NileChain.AI.Evaluation;
using NileChain.AI.Models;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Sbg;
using NileChain.AI.Telemetry;
using NileChain.AI.Matching;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Tests;

/// <summary>
/// Runs the golden set through the real deterministic pipeline (no LLM configured) and grades
/// every scenario with <see cref="GoldenSetEvaluator"/>. One assertion, one readable report:
/// a regression names the scenario and the exact expectation it broke.
/// </summary>
public class GoldenSetEvalTests
{
    [Fact]
    public async Task GoldenSet_AllScenariosPass()
    {
        var runs = new List<(GoldenScenario, AgentResponse)>();

        foreach (var scenario in GoldenSet.All)
        {
            var response = await RunAsync(scenario);

            // Guards the harness itself: a graded run must have gone through the pipeline,
            // not returned an empty shell that happens to match a zero-count expectation.
            Assert.Contains("Fallback", response.OrchestratorMode);
            Assert.NotEmpty(response.ToolCallTrail);

            runs.Add((scenario, response));
        }

        var report = GoldenSetEvaluator.Evaluate(runs);

        Assert.True(report.Passed, Environment.NewLine + report.Format());
    }

    [Fact]
    public void GoldenSet_CoversEveryGeographicScope()
    {
        var scopes = GoldenSet.All.Select(s => s.World.GeoScope).Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(3, scopes.Count());
    }

    [Fact]
    public void Evaluator_ReportsEveryBrokenExpectation()
    {
        var scenario = GoldenSet.All.First(s => s.Name == "exact-stays-exact");

        // A run that leaked a neighbour into the shortlist and skipped the risk tool.
        var leaked = new AgentResponse
        {
            Success = true,
            TotalEligible = 2,
            TruncatedCount = 0,
            TopMatches =
            [
                new MatchResult { FarmName = "Giza Unverified", Governorate = "Giza" },
                new MatchResult { FarmName = "Qalyubia Verified", Governorate = "Qalyubia" }
            ],
            ToolCallTrail = [new ToolCallTrailEntry { FunctionName = "SearchFarms(deterministic)" }]
        };

        var result = GoldenSetEvaluator.Evaluate(scenario, leaked);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, f => f.Contains("geographic scope breached"));
        Assert.Contains(result.Failures, f => f.Contains("missing CalculateRiskScore"));
        Assert.Contains(result.Failures, f => f.Contains("topMatches"));
        Assert.Contains(result.Failures, f => f.Contains("peek hint"));
    }

    [Fact]
    public void Evaluator_BlockedGuardrailCallIsNotAViolation()
    {
        var scenario = GoldenSet.All.First(s => s.Name == "nationwide-reaches-everywhere-without-a-peek");
        var expectation = scenario.Expect with { ForbiddenTrailFunctions = [GoldenSet.WidenSearchRadius] };

        var blocked = new AgentResponse
        {
            Success = true,
            TotalEligible = 2,
            TruncatedCount = 0,
            TopMatches =
            [
                new MatchResult { FarmName = "Giza Farm", Governorate = "Giza" },
                new MatchResult { FarmName = "Aswan Farm", Governorate = "Aswan" }
            ],
            ToolCallTrail =
            [
                new ToolCallTrailEntry { FunctionName = "SearchFarms" },
                new ToolCallTrailEntry { FunctionName = "CalculateRiskScore" },
                new ToolCallTrailEntry { FunctionName = "WidenSearchRadius", Blocked = true }
            ]
        };

        var result = GoldenSetEvaluator.Evaluate(scenario with { Expect = expectation }, blocked);

        Assert.True(result.Passed, string.Join("; ", result.Failures));
    }

    [Fact]
    public void Evaluator_CatchesCountsThatDoNotAddUp()
    {
        var scenario = GoldenSet.All.First(s => s.Name == "shortlist-cap-reports-what-it-hid");

        var inconsistent = new AgentResponse
        {
            Success = true,
            TotalEligible = scenario.Expect.TotalEligible,
            TruncatedCount = 0,
            TopMatches = [.. Enumerable.Range(0, MatchingLimits.DefaultMaxResults).Select(i => new MatchResult
            {
                FarmName = $"Giza {i}",
                Governorate = "Giza"
            })],
            ToolCallTrail =
            [
                new ToolCallTrailEntry { FunctionName = "SearchFarms" },
                new ToolCallTrailEntry { FunctionName = "CalculateRiskScore" }
            ]
        };

        var result = GoldenSetEvaluator.Evaluate(scenario, inconsistent);

        Assert.Contains(result.Failures, f => f.Contains("counts do not add up"));
    }

    private static async Task<AgentResponse> RunAsync(GoldenScenario scenario)
    {
        await using var db = CreateDb();
        var requestId = Seed(db, scenario.World);
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);

        return await orchestrator.RunAsync(new AgentRequest
        {
            RequestId = requestId,
            CropType = scenario.World.Crop,
            QuantityTons = scenario.World.QuantityTons,
            QualitySpecs = scenario.World.QualitySpecs,
            PricePerTon = 10_000m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(45),
            FactoryGovernorate = scenario.World.FactoryGovernorate
        });
    }

    /// <summary>
    /// The LLM is switched off explicitly rather than by omission: provider keys in the developer's
    /// environment must not turn a graded run into a live model call.
    /// </summary>
    private static OrchestratorAgent CreateOrchestrator(NileChainDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Provider"] = LlmKernelFactory.ProviderNone
            })
            .Build();

        var matchingPlugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var riskPlugin = new RiskPlugin(db);
        var sbg = new SbgStudentChatClient(
            new HttpClient(),
            Options.Create(new SbgOptions()),
            NullLogger<SbgStudentChatClient>.Instance);

        var kernelProvider = new OpenAiKernelProvider(
            kernel: null,
            unavailableReason: "golden-set run is deterministic by design",
            supportsNativeToolCalling: false,
            providerName: "None");

        var contractAgent = new Lazy<ContractAgent>(() => new ContractAgent(
            kernelProvider,
            new ContractPlugin(),
            new RagPipeline(new ChromaService(new HttpClient { BaseAddress = new Uri("http://chroma.invalid") })),
            configuration,
            sbg,
            new LlmUsageLedger(),
            NullLogger<ContractAgent>.Instance));

        return new OrchestratorAgent(
            new MatchingAgent(matchingPlugin),
            new RiskAgent(riskPlugin),
            matchingPlugin,
            riskPlugin,
            contractAgent,
            kernelProvider,
            sbg,
            configuration,
            db,
            NullLogger<OrchestratorAgent>.Instance,
            new LlmUsageLedger(),
            LlmPricing.Empty());
    }

    private static Guid Seed(NileChainDbContext db, GoldenWorld world)
    {
        var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = world.Crop };
        db.CropTypes.Add(crop);

        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Golden Factory",
            Governorate = world.FactoryGovernorate,
            CreatedAt = DateTime.UtcNow
        };
        db.Factory.Add(factory);

        var request = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = world.QuantityTons,
            QualitySpecs = world.QualitySpecs,
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            FactoryApprovedOneRingExpansion = world.FactoryApprovedOneRingExpansion,
            ShortlistTakeLimit = world.ShortlistTakeLimit
        };
        db.SupplyRequests.Add(request);

        foreach (var farm in world.Farms)
            SeedFarm(db, farm, crop);

        return request.RequestId;
    }

    /// <summary>
    /// Builds a profile that genuinely computes to the requested band, and stores the same value
    /// in the cached column so pre- and post-recompute ranking agree.
    /// </summary>
    private static void SeedFarm(NileChainDbContext db, GoldenFarm spec, CropType crop)
    {
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var rich = spec.Trust != FarmTrustBand.Low;

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{spec.Name}@golden.test",
            Email = $"{spec.Name}@golden.test",
            PhoneNumber = rich ? "01000000000" : null
        };
        db.Users.Add(user);

        var farm = new Farm
        {
            FarmId = farmId,
            UserId = userId,
            Name = spec.Name,
            Governorate = spec.Governorate,
            IsVerified = spec.Verified,
            ProfileComplete = true,
            CreatedAt = DateTime.UtcNow,
            User = user,
            Location = rich ? $"{spec.Governorate} rural road" : null,
            SizeInFeddans = rich ? 120m : null,
            Description = rich ? "مزرعة اختبار للمجموعة الذهبية." : null,
            FarmCrops =
            [
                new FarmCrop
                {
                    CropTypeId = crop.CropTypeId,
                    CropType = crop,
                    AvailableQuantityTons = spec.AvailableTons
                }
            ]
        };

        if (rich)
        {
            farm.FarmDocuments =
            [
                new FarmDocument
                {
                    FarmDocumentId = Guid.NewGuid(),
                    FarmId = farmId,
                    FileName = "license.pdf",
                    FileUrl = "https://golden.test/license.pdf",
                    FileType = "application/pdf",
                    PublicId = $"golden/{farmId:N}/license"
                }
            ];
            farm.FarmImages =
            [
                new FarmImage
                {
                    FarmImageId = Guid.NewGuid(),
                    FarmId = farmId,
                    FileName = "field.jpg",
                    FileUrl = "https://golden.test/field.jpg",
                    PublicId = $"golden/{farmId:N}/field"
                }
            ];
            farm.FarmCertifications = BuildCertifications(db, farmId);
        }

        // A top band also needs a perfect rating history, which is what pushes it past 70.
        if (spec.Trust == FarmTrustBand.High)
        {
            for (var i = 0; i < 2; i++)
            {
                db.Reviews.Add(new Review
                {
                    ReviewId = Guid.NewGuid(),
                    ContractId = Guid.NewGuid(),
                    ReviewerId = Guid.NewGuid(),
                    TargetId = userId,
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        farm.RiskScore = FarmTrustScore.Compute(
            FarmTrustScore.InputsFrom(
                farm,
                signedContractCount: 0,
                averageRating: spec.Trust == FarmTrustBand.High ? 5m : 0m,
                DateTime.UtcNow)).Overall;

        db.Farm.Add(farm);
    }

    private static List<FarmCertification> BuildCertifications(NileChainDbContext db, Guid farmId)
    {
        var admin = Guid.NewGuid();
        var certifications = new List<FarmCertification>();

        foreach (var name in new[] { "GlobalG.A.P.", "ISO 22000" })
        {
            var certification = new Certification { CertificationId = Guid.NewGuid(), Name = name };
            db.Certifications.Add(certification);

            certifications.Add(new FarmCertification
            {
                FarmId = farmId,
                CertificationId = certification.CertificationId,
                Certification = certification,
                IssuedAt = DateTime.UtcNow.AddMonths(-2),
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                GrantedByAdminUserId = admin
            });
        }

        return certifications;
    }

    private static NileChainDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NileChainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NileChainDbContext(options);
    }
}
