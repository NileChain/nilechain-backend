namespace NileChain.Domain.Enums;

public enum SubscriptionStatus
{
    Active = 0,
    Expired = 1,
    Cancelled = 2
}

public enum SubscriptionSource
{
    Wallet = 0,
    AdminGrant = 1
}

public enum SubscriptionMetric
{
    FactoryRfqs = 0,
    AgentRuns = 1,
    FarmAccepts = 2
}
