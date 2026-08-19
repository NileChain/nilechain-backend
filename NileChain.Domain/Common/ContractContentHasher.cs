using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NileChain.Domain.Entities;

namespace NileChain.Domain.Common;

/// <summary>
/// SHA-256 of canonical JSON with sorted keys. Independent of the integrity chain hasher.
/// </summary>
public static class ContractContentHasher
{
    public static string Compute(CanonicalContractPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var dict = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["contractId"] = payload.ContractId.ToString("D").ToLowerInvariant(),
            ["cropName"] = Norm(payload.CropName),
            ["deliveryDate"] = payload.DeliveryDate?.ToUniversalTime()
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            ["farmName"] = Norm(payload.FarmName),
            ["factoryName"] = Norm(payload.FactoryName),
            ["generatedText"] = payload.GeneratedText ?? "",
            ["matchId"] = payload.MatchId.ToString("D").ToLowerInvariant(),
            ["pricePerTon"] = payload.PricePerTon,
            ["quantityTons"] = payload.QuantityTons
        };

        var json = JsonSerializer.Serialize(dict, SerializerOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static CanonicalContractPayload FromContract(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var match = contract.FarmMatch;
        var supply = match?.SupplyRequest;
        return new CanonicalContractPayload(
            contract.ContractId,
            contract.MatchId,
            match?.Farm?.Name ?? "",
            supply?.Factory?.Name ?? "",
            supply?.CropType?.Name ?? "",
            match is null ? 0m : MatchCommercialTerms.QuantityTons(match),
            match is null ? 0m : MatchCommercialTerms.PricePerTon(match) ?? 0m,
            match is null ? null : MatchCommercialTerms.DeliveryDate(match),
            contract.GeneratedText);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}
