using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NileChain.Domain.Common;

/// <summary>
/// SHA-256 hasher for signed-contract integrity (platform chain, not payment settlement).
/// </summary>
public static class ContractIntegrityHasher
{
    public const string GenesisPreviousHash =
        "0000000000000000000000000000000000000000000000000000000000000000";

    public static string ComputeContentHash(ContractIntegrityPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var canonical = string.Join('\n',
            payload.ContractId.ToString("N"),
            Norm(payload.FarmName),
            Norm(payload.FactoryName),
            Norm(payload.CropName),
            payload.QuantityTons.ToString("0.####", CultureInfo.InvariantCulture),
            payload.PricePerTon.ToString("0.####", CultureInfo.InvariantCulture),
            payload.DeliveryDate?.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            payload.GeneratedText ?? "",
            payload.FarmSignedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            payload.FactorySignedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ShortHash(string contentHash, int length = 12)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
            return string.Empty;
        var hex = contentHash.Trim().ToLowerInvariant();
        return hex.Length <= length ? hex : hex[..length];
    }

    public static string BuildTxRef(long chainIndex, string contentHash) =>
        $"NC-{chainIndex:D6}-{ShortHash(contentHash, 12)}";

    private static string Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}
