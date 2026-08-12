namespace NileChain.Application.Options;

/// <summary>Paymob Accept (Egypt) for wallet top-ups.</summary>
public class PaymobOptions
{
    public const string SectionName = "Paymob";

    /// <summary>When true and keys are set, top-ups use Paymob Intention + Unified Checkout.</summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://accept.paymob.com";
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string HmacSecret { get; set; } = string.Empty;

    /// <summary>Card integration ID (test or live matching the secret key mode).</summary>
    public int CardIntegrationId { get; set; }

    /// <summary>
    /// When Paymob keys are missing, allow a localhost simulator that completes
    /// top-up without a real card redirect. Keep false for a natural gateway UX.
    /// </summary>
    public bool AllowLocalSimulator { get; set; } = false;

    /// <summary>Public API base used for notification_url (e.g. https://xxx.ngrok.io).</summary>
    public string? PublicApiBaseUrl { get; set; }
}
