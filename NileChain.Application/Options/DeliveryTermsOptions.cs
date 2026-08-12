namespace NileChain.Application.Options;

public sealed class DeliveryTermsOptions
{
    public const string SectionName = "DeliveryTerms";

    /// <summary>When true, create-request without deliveryPoint is 400. Graduation default is false.</summary>
    public bool Required { get; set; }

    public string DefaultPoint { get; set; } = "FactoryGate";
}
