namespace NileChain.Application.Dtos.Farm;

public class CounterOfferRequest
{
    public decimal? QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Note { get; set; }
    public string? Grade { get; set; }
}
