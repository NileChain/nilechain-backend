namespace NileChain.Application.Options;

public sealed class DisputeOptions
{
    public const string SectionName = "Disputes";

    /// <summary>Hours after open before an active dispute is marked overdue. Does not auto-resolve.</summary>
    public int SlaHours { get; set; } = 48;
}
