namespace NileChain.Domain.Entities;

public class ComparisonReport
{
    public Guid ReportId { get; set; }
    public Guid RequestId { get; set; }
    public string? GeneratedText { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SupplyRequest SupplyRequest { get; set; } = default!;
}
