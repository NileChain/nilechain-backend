namespace NileChain.API.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Explicit browser origins allowed by CORS. Never use AllowAnyOrigin with credentials.
    /// Override in Production with Cors__Origins (comma-separated) — scalar wins over appsettings array —
    /// or Cors__Origins__0 / Cors__Origins__1 indexed keys.
    /// </summary>
    public string[] Origins { get; set; } =
    [
        "http://localhost:4200",
        "http://127.0.0.1:4200"
    ];
}
