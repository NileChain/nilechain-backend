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
}
