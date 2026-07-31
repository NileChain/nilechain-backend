namespace NileChain.AI.Models;

public sealed class ContractGenerationResult
{
    public bool Success { get; init; }
    public string ContractText { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ContractGenerationResult Ok(string contractText) => new()
    {
        Success = true,
        ContractText = contractText
    };

    public static ContractGenerationResult Unavailable(string message) => new()
    {
        Success = false,
        ErrorCode = "AI.ServiceUnavailable",
        ErrorMessage = message
    };
}
