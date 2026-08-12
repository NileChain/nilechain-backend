using NileChain.Domain.Entities;
using NileChain.Domain.Matching;

namespace NileChain.Application.Matching;

public static class MatchEligibilityGuard
{
    /// <summary>
    /// Returns false when the live farm profile drifted materially from the propose-time snapshot.
    /// Missing snapshot = pass (legacy rows).
    /// </summary>
    public static bool IsStillEligible(FarmMatch? match)
    {
        if (match is null)
            return true;

        var snapshot = MatchEligibilitySnapshot.TryParse(match.EligibilitySnapshotJson);
        if (snapshot is null)
            return true;

        var farm = match.Farm;
        if (farm is null)
            return true;

        var request = match.SupplyRequest;
        var requestCropId = request?.CropTypeId;
        var cropForRequest = requestCropId is Guid cid
            ? farm.FarmCrops?.FirstOrDefault(c => c.CropTypeId == cid)
            : null;

        var live = new MatchEligibilitySnapshot
        {
            Governorate = farm.Governorate,
            CropTypeIds = farm.FarmCrops?.Select(c => c.CropTypeId).ToList() ?? new List<Guid>(),
            AvailableQuantityTons = cropForRequest?.AvailableQuantityTons,
            MinPricePerTon = cropForRequest?.MinPricePerTon,
            RiskScore = farm.RiskScore,
            IsVerified = farm.IsVerified,
            DeliveryPoint = request?.DeliveryPoint.ToString(),
            FreightPayer = request?.FreightPayer.ToString(),
            TransitRisk = request?.TransitRisk.ToString()
        };

        return !MatchEligibilitySnapshot.HasMaterialDrift(
            snapshot,
            live,
            request?.QuantityTons,
            request?.PricePerTon);
    }
}
