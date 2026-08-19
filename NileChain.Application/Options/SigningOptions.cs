namespace NileChain.Application.Options;

public sealed class SigningOptions
{
    public const string SectionName = "Signing";

    /// <summary>HMAC-SHA256 secret for signature tokens. Load from configuration only.</summary>
    public string HmacSecret { get; set; } = string.Empty;
}
