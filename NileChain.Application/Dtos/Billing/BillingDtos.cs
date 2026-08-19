namespace NileChain.Application.Dtos.Billing;

public class BillingMeDto
{
    public string Role { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal ProPriceEgp { get; set; }
    public bool IsPro { get; set; }
    public bool Copilot { get; set; }
    public bool ShowMore { get; set; }
    public bool ExpandGeo { get; set; }
    public BillingMeterDto FactoryRfqs { get; set; } = new();
    public BillingMeterDto AgentRuns { get; set; } = new();
    public BillingMeterDto FarmAccepts { get; set; } = new();
    public string HonestyNote { get; set; } =
        "Monthly quota funded from the NileChain wallet — not a bank standing order and not live card billing.";
}

public class BillingMeterDto
{
    public string Metric { get; set; } = string.Empty;
    public int Used { get; set; }
    public int? Cap { get; set; }
    public int? Remaining { get; set; }
}

public class AdminSubscriptionGrantRequest
{
    public string PlanCode { get; set; } = string.Empty;
    public DateTime? PeriodEndUtc { get; set; }
}
