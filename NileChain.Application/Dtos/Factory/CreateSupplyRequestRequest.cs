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

    /// <summary>Optional structured QC expectations (moisture, impurities, grade, lab).</summary>
    public StructuredQualityInput? StructuredQuality { get; set; }

    /// <summary>Optional farm preferred from a published listing (RFQ context).</summary>
    public Guid? PreferredFarmId { get; set; }

    /// <summary>Optional idempotency key (also accepted via Idempotency-Key header).</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>FarmGate | FactoryGate. Defaults to FactoryGate.</summary>
    public string? DeliveryPoint { get; set; }
    /// <summary>Farm | Factory. Defaults from delivery point.</summary>
    public string? FreightPayer { get; set; }
    /// <summary>Farm | Factory. Defaults from delivery point.</summary>
    public string? TransitRisk { get; set; }
}

public class UpdateSupplyRequestDeliveryTermsRequest
{
    public string? DeliveryPoint { get; set; }
    public string? FreightPayer { get; set; }
    public string? TransitRisk { get; set; }
}

public class CreateSupplyRequestResponse
{
    public Guid RequestId { get; set; }
}
