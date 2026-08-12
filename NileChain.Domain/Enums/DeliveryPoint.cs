namespace NileChain.Domain.Enums;

/// <summary>Where the goods change hands. Not full Incoterms.</summary>
public enum DeliveryPoint
{
    /// <summary>Factory collects at the farm. Factory typically pays freight and bears transit.</summary>
    FarmGate = 0,
    /// <summary>Farm delivers to the plant. Farm typically pays freight and bears transit.</summary>
    FactoryGate = 1
}
