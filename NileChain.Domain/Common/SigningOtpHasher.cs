using System.Security.Cryptography;
using System.Text;

namespace NileChain.Domain.Common;

/// <summary>SHA-256 hashing for signing OTPs. Never log the plaintext code.</summary>
public static class SigningOtpHasher
{
    public static string Hash(string otpCode)
    {
        ArgumentNullException.ThrowIfNull(otpCode);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otpCode.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool Matches(string storedHex, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(storedHex) || otpCode is null)
            return false;

        byte[] stored;
        try
        {
            stored = Convert.FromHexString(storedHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var computed = Convert.FromHexString(Hash(otpCode));
        if (stored.Length != computed.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(stored, computed);
    }
}
