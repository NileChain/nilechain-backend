using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Application.Common;

public static class MatchNegotiationWriter
{
    public static MatchNegotiationRound Append(
        FarmMatch match,
        DealParty offeredBy,
        decimal? quantityTons,
        decimal? pricePerTon,
        DateTime? deliveryDate,
        string? note,
        string? grade = null)
    {
        match.CounterQuantityTons = quantityTons ?? match.CounterQuantityTons;
        match.CounterPricePerTon = pricePerTon ?? match.CounterPricePerTon;
        match.CounterDeliveryDate = deliveryDate ?? match.CounterDeliveryDate;
        match.CounterNote = string.IsNullOrWhiteSpace(note) ? match.CounterNote : note.Trim();
        match.CounteredAt = DateTime.UtcNow;
        match.CounterAccepted = false;
        match.Status = FarmMatchStatus.Countered;

        var round = new MatchNegotiationRound
        {
            RoundId = Guid.NewGuid(),
            MatchId = match.MatchId,
            OfferedBy = offeredBy,
            QuantityTons = quantityTons,
            PricePerTon = pricePerTon,
            DeliveryDate = deliveryDate,
            Grade = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        match.NegotiationRounds.Add(round);
        return round;
    }
}
