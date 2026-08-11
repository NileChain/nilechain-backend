namespace NileChain.Application.Interfaces;

public interface IContractPdfService
{
    byte[] GeneratePdf(
        string title,
        string contractText,
        string farmName,
        string factoryName,
        bool factorySigned = false,
        bool farmSigned = false,
        DateTime? factorySignedAt = null,
        DateTime? farmSignedAt = null);
}
