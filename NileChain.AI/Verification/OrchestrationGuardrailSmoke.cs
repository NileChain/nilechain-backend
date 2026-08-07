using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Models;
using NileChain.AI.Orchestration;
using NileChain.AI.Plugins;

/// <summary>
/// Offline guardrail smoke checks (no OpenAI / no DB).
/// Run: dotnet run --project NileChain.AI -- (or invoke via script below).
/// This file is a standalone verification helper compiled only when defining VERIFY_ORCHESTRATION.
/// </summary>
namespace NileChain.AI.Verification;

public static class OrchestrationGuardrailSmoke
{
    public static void Run()
    {
        Console.WriteLine("=== Orchestration guardrail smoke (no LLM) ===");

        var state = new OrchestrationRunState
        {
            RequestId = Guid.NewGuid(),
            Request = new AgentRequest
            {
                CropType = "Wheat",
                QuantityTons = 100,
                PricePerTon = 10000,
                DeliveryDate = DateTime.UtcNow.AddMonths(2),
                QualitySpecs = "Grade A",
                FactoryGovernorate = "Aswan"
            }
        };

        // Cap: WidenSearchRadius twice → second blocked
        var plugin = new OrchestrationToolsPlugin(
            matchingPlugin: null!,
            riskPlugin: null!,
            contractAgent: null!,
            db: null!,
            logger: NullLogger.Instance,
            state: state);

        // Directly test widen/propose/flag/validate without DB tools.
        var w1 = plugin.WidenSearchRadius(50);
        var w2 = plugin.WidenSearchRadius(100);
        Console.WriteLine("Widen #1: " + w1);
        Console.WriteLine("Widen #2 (expect blocked): " + w2);
        Console.WriteLine($"PartialResult after widens: {state.PartialResult}");

        state.RankedCandidates.Add(new MatchResult
        {
            FarmId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FarmName = "Farm A",
            MatchScore = 90,
            RiskScore = 80
        });
        state.RankedCandidates.Add(new MatchResult
        {
            FarmId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FarmName = "Farm B",
            MatchScore = 70,
            RiskScore = 60
        });

        var p1 = plugin.ProposeNextBestMatch(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            state.RequestId);
        var p2 = plugin.ProposeNextBestMatch(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            state.RequestId);
        Console.WriteLine("Propose #1: " + p1);
        Console.WriteLine("Propose #2 (expect blocked): " + p2);

        var warn = plugin.FlagLowRiskWarning(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), 25);
        Console.WriteLine("FlagLowRisk: " + warn);
        Console.WriteLine($"RiskWarningActive={state.RiskWarningActive}, Confirmed={state.FactoryConfirmedHighRisk}");

        // Contract validation
        var ok = OrchestrationToolsPlugin.ValidateContractFields(
            """
            بسم الله الرحمن الرحيم
            الطرف الأول (المورد): Farm A
            الطرف الثاني (المشتري): Factory X
            المحصول: Wheat
            الكمية: 100 طن
            السعر: 10000 جنيه
            تاريخ التسليم: 01 December 2026
            """,
            state.Request,
            "Farm A",
            "Factory X",
            out var err);
        Console.WriteLine($"Validate complete contract: {ok} ({err})");

        var bad = OrchestrationToolsPlugin.ValidateContractFields(
            "incomplete draft",
            state.Request,
            "Farm A",
            "Factory X",
            out var err2);
        Console.WriteLine($"Validate incomplete contract: {bad} ({err2})");

        Console.WriteLine("=== Trail ===");
        foreach (var t in state.Trail)
            Console.WriteLine($"{t.FunctionName}: {t.ResultSummary}");
    }
}
