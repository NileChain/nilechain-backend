using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ComparisonReportConfiguration : IEntityTypeConfiguration<ComparisonReport>
{
    public void Configure(EntityTypeBuilder<ComparisonReport> builder)
    {
        builder.ToTable("ComparisonReport");
        builder.HasKey(r => r.ReportId);

        builder.HasOne(r => r.SupplyRequest)
            .WithMany(s => s.ComparisonReports)
            .HasForeignKey(r => r.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
