namespace NileChain.Domain.Entities;

public class RiskAssessmentReport
{
    public Guid ReportId { get; set; }
    public Guid FarmId { get; set; }
    public string? GeneratedText { get; set; }
    public string? FactorsBreakdown { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Farm Farm { get; set; } = default!;
}
