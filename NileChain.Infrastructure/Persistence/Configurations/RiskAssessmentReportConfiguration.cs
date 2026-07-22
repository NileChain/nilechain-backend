using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class RiskAssessmentReportConfiguration : IEntityTypeConfiguration<RiskAssessmentReport>
{
    public void Configure(EntityTypeBuilder<RiskAssessmentReport> builder)
    {
        builder.ToTable("RiskAssessmentReport");
        builder.HasKey(r => r.ReportId);

        builder.HasOne(r => r.Farm)
            .WithMany(f => f.RiskAssessmentReports)
            .HasForeignKey(r => r.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
