using NileChain.Application.Dtos.Contracts;

namespace NileChain.Application.Interfaces;

public interface IContractPdfService
{
    /// <summary>
    /// Renders a structured A4 agricultural supply agreement PDF.
    /// Legal wording is taken from <see cref="ContractPdfModel.GeneratedText"/> only.
    /// </summary>
    byte[] GeneratePdf(ContractPdfModel model);

    /// <summary>
    /// Legacy overload kept for older tests/callers. Prefer <see cref="GeneratePdf(ContractPdfModel)"/>.
    /// </summary>
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
