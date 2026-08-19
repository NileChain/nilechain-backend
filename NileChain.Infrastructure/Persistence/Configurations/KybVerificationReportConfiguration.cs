using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class KybVerificationReportConfiguration : IEntityTypeConfiguration<KybVerificationReport>
{
    public void Configure(EntityTypeBuilder<KybVerificationReport> builder)
    {
        builder.ToTable("KybVerificationReport");
        builder.HasKey(r => r.ReportId);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.TrustScore).IsRequired();
        builder.Property(r => r.OverallSummary).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.BreakdownJson).HasMaxLength(100000).IsRequired();
        builder.Property(r => r.Recommendation).HasMaxLength(20).IsRequired();
    }
}

