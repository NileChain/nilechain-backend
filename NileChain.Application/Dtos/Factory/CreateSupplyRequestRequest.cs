namespace NileChain.Application.Dtos.Factory;

public class CreateSupplyRequestRequest
{
    public string Crop { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string? Quality { get; set; }
    public List<string> SelectedGovernorates { get; set; } = new();
}

public class CreateSupplyRequestResponse
{
    public Guid RequestId { get; set; }
}
