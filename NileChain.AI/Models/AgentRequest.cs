namespace NileChain.AI.Models;

public class AgentRequest
{
    public Guid RequestId { get; set; }
    public string CropType { get; set; } = string.Empty;
    public decimal QuantityTons { get; set; }
    public string QualitySpecs { get; set; } = string.Empty;
    public decimal PricePerTon { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string FactoryGovernorate { get; set; } = string.Empty;
    public string DeliveryPoint { get; set; } = "FactoryGate";
    public string FreightPayer { get; set; } = "Farm";
    public string TransitRisk { get; set; } = "Farm";

    /// <summary>
    /// When true, the factory has acknowledged a prior low-risk warning and
    /// GenerateContract may proceed despite FlagLowRiskWarning having fired.
    /// </summary>
    public bool ConfirmHighRiskWarning { get; set; }
}
