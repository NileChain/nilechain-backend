namespace NileChain.Application.Dtos.Factory;

public class CreateSupplyRequestRequest
{
    public string Crop { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string? Quality { get; set; }
    public List<string> SelectedGovernorates { get; set; } = new();
    /// <summary>Exact | Nearby | Nationwide. Defaults to Exact when governorates are selected.</summary>
    public string? GeographicScope { get; set; }

    /// <summary>Optional idempotency key (also accepted via Idempotency-Key header).</summary>
    public string? IdempotencyKey { get; set; }
}

public class CreateSupplyRequestResponse
{
    public Guid RequestId { get; set; }
}
