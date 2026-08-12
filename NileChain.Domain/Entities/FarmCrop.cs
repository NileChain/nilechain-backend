namespace NileChain.Domain.Entities;

/// <summary>
/// Farm crop listing with commercial availability (quantity, season, floor price).
/// </summary>
public class FarmCrop
{
    public Guid FarmId { get; set; }
    public Guid CropTypeId { get; set; }

    /// <summary>Tons currently available to sell. Null = unknown (still match-eligible).</summary>
    public decimal? AvailableQuantityTons { get; set; }

    /// <summary>Inclusive start of harvest / delivery window. Null = no seasonal constraint.</summary>
    public DateTime? AvailableFrom { get; set; }

    /// <summary>Inclusive end of harvest / delivery window. Null = no seasonal constraint.</summary>
    public DateTime? AvailableTo { get; set; }

    /// <summary>Minimum acceptable price per ton (EGP). Null = no floor price.</summary>
    public decimal? MinPricePerTon { get; set; }

    /// <summary>When true, crop appears on factory marketplace listings browse.</summary>
    public bool IsPublished { get; set; } = true;

    public Farm Farm { get; set; } = default!;
    public CropType CropType { get; set; } = default!;
}
