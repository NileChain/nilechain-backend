namespace NileChain.Application.Common;

/// <summary>
/// Stable, client-safe error messages — never forward raw exception text.
/// </summary>
public static class ClientErrorSanitizer
{
    public const string AgentFailureCode = "AI.OrchestratorFailed";
    public const string AgentFailureMessage = "The AI agent could not complete this request. Please try again.";
    public const string ServiceUnavailableCode = "AI.ServiceUnavailable";
    public const string ServiceUnavailableMessage = "AI service unavailable";
    public const string GenericServerCode = "Server.InternalError";
    public const string GenericServerMessage = "An unexpected error occurred.";

    public static string SanitizeTrailText(string? text, int maxLength = 240)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var trimmed = text.Trim();

        // Drop stack-trace / exception type dumps from tool trails.
        if (trimmed.Contains("Exception:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(" at ", StringComparison.Ordinal)
            || trimmed.Contains("StackTrace", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("error=", StringComparison.OrdinalIgnoreCase))
        {
            return "An internal error occurred during this step.";
        }

        if (trimmed.Length <= maxLength)
            return trimmed;

        return trimmed[..maxLength] + "…";
    }
}
