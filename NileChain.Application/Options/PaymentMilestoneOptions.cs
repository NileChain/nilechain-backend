namespace NileChain.Application.Options;

/// <summary>
/// Configurable payment milestone schedule (status tracking labels/percents only).
/// </summary>
public sealed class PaymentMilestoneOptions
{
    public const string SectionName = "PaymentMilestones";

    /// <summary>Default aligns with generated contract text: 30% advance / 70% on delivery.</summary>
    public List<PaymentMilestoneStepOptions> Schedule { get; set; } =
    [
        new() { Key = "Deposit", Label = "Deposit (advance)", Percent = 30 },
        new() { Key = "OnDelivery", Label = "On delivery", Percent = 70 }
    ];
}

public sealed class PaymentMilestoneStepOptions
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Percent { get; set; }
}
