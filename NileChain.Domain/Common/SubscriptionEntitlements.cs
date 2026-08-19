using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Caps and feature flags for a marketplace plan. Null cap = unlimited.
/// Prices and free-tier numbers come from config so the viva can change them
/// without a migration; this helper is the single place flags are resolved.
/// </summary>
public sealed record SubscriptionEntitlements(
    string PlanCode,
    int? FactoryRfqs,
    int? AgentRuns,
    int? FarmAccepts,
    bool Copilot,
    bool ShowMore,
    bool ExpandGeo,
    decimal MonthlyPriceEgp)
{
    public int? CapFor(SubscriptionMetric metric) => metric switch
    {
        SubscriptionMetric.FactoryRfqs => FactoryRfqs,
        SubscriptionMetric.AgentRuns => AgentRuns,
        SubscriptionMetric.FarmAccepts => FarmAccepts,
        _ => 0
    };

    public bool Flag(SubscriptionFeatureFlag flag) => flag switch
    {
        SubscriptionFeatureFlag.Copilot => Copilot,
        SubscriptionFeatureFlag.ShowMore => ShowMore,
        SubscriptionFeatureFlag.ExpandGeo => ExpandGeo,
        _ => false
    };
}

public enum SubscriptionFeatureFlag
{
    Copilot = 0,
    ShowMore = 1,
    ExpandGeo = 2
}

public readonly record struct SubscriptionPlanConfig(
    int FreeFactoryRfqs,
    int FreeFactoryAgentRuns,
    int FreeFarmAccepts,
    int? ProFactoryRfqs,
    int? ProFactoryAgentRuns,
    int? ProFarmAccepts,
    decimal FactoryProEgp,
    decimal FarmProEgp);

public static class SubscriptionEntitlementsResolver
{
    public static SubscriptionPlanConfig DefaultConfig { get; } = new(
        FreeFactoryRfqs: 2,
        FreeFactoryAgentRuns: 2,
        FreeFarmAccepts: 3,
        ProFactoryRfqs: 20,
        ProFactoryAgentRuns: null,
        ProFarmAccepts: null,
        FactoryProEgp: 1500m,
        FarmProEgp: 800m);

    public static SubscriptionEntitlements For(string? planCode, SubscriptionPlanConfig? config = null)
    {
        var cfg = config ?? DefaultConfig;
        var code = (planCode ?? string.Empty).Trim().ToLowerInvariant();

        return code switch
        {
            SubscriptionPlanCodes.FactoryPro => new SubscriptionEntitlements(
                SubscriptionPlanCodes.FactoryPro,
                FactoryRfqs: cfg.ProFactoryRfqs,
                AgentRuns: cfg.ProFactoryAgentRuns,
                FarmAccepts: null,
                Copilot: true,
                ShowMore: true,
                ExpandGeo: true,
                MonthlyPriceEgp: cfg.FactoryProEgp),
            SubscriptionPlanCodes.FarmPro => new SubscriptionEntitlements(
                SubscriptionPlanCodes.FarmPro,
                FactoryRfqs: null,
                AgentRuns: null,
                FarmAccepts: cfg.ProFarmAccepts,
                Copilot: false,
                ShowMore: false,
                ExpandGeo: false,
                MonthlyPriceEgp: cfg.FarmProEgp),
            SubscriptionPlanCodes.FarmFree => FarmFree(cfg),
            _ => FactoryFree(cfg)
        };
    }

    public static SubscriptionEntitlements FactoryFree(SubscriptionPlanConfig? config = null)
    {
        var cfg = config ?? DefaultConfig;
        return new SubscriptionEntitlements(
            SubscriptionPlanCodes.FactoryFree,
            FactoryRfqs: cfg.FreeFactoryRfqs,
            AgentRuns: cfg.FreeFactoryAgentRuns,
            FarmAccepts: null,
            Copilot: false,
            ShowMore: false,
            ExpandGeo: false,
            MonthlyPriceEgp: 0m);
    }

    public static SubscriptionEntitlements FarmFree(SubscriptionPlanConfig? config = null)
    {
        var cfg = config ?? DefaultConfig;
        return new SubscriptionEntitlements(
            SubscriptionPlanCodes.FarmFree,
            FactoryRfqs: null,
            AgentRuns: null,
            FarmAccepts: cfg.FreeFarmAccepts,
            Copilot: false,
            ShowMore: false,
            ExpandGeo: false,
            MonthlyPriceEgp: 0m);
    }

    public static SubscriptionEntitlements FreeForRole(bool asFarm, SubscriptionPlanConfig? config = null) =>
        asFarm ? FarmFree(config) : FactoryFree(config);
}
