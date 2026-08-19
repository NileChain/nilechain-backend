namespace NileChain.Application.Options;

/// <summary>Marketplace access-quota plans. EGP prices live in config, not migrations.</summary>
public class SubscriptionOptions
{
    public const string SectionName = "Subscriptions";

    public decimal FactoryProEgp { get; set; } = 1500m;
    public decimal FarmProEgp { get; set; } = 800m;
    public int FreeFactoryRfqs { get; set; } = 2;
    public int FreeFactoryAgentRuns { get; set; } = 2;
    public int FreeFarmAccepts { get; set; } = 3;
    public int ProFactoryRfqs { get; set; } = 20;
    /// <summary>Null or omitted = unlimited.</summary>
    public int? ProFactoryAgentRuns { get; set; }
    public int? ProFarmAccepts { get; set; }

    public NileChain.Domain.Common.SubscriptionPlanConfig ToPlanConfig() => new(
        FreeFactoryRfqs,
        FreeFactoryAgentRuns,
        FreeFarmAccepts,
        ProFactoryRfqs,
        ProFactoryAgentRuns,
        ProFarmAccepts,
        FactoryProEgp,
        FarmProEgp);
}
