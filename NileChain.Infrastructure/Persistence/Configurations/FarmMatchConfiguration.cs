using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FarmMatchConfiguration : IEntityTypeConfiguration<FarmMatch>
{
    public void Configure(EntityTypeBuilder<FarmMatch> builder)
    {
        builder.ToTable("FarmMatch");
        builder.HasKey(m => m.MatchId);
        builder.Property(m => m.MatchScore).HasPrecision(5, 2);
        builder.Property(m => m.RiskScore).HasPrecision(5, 2);
        builder.Property(m => m.MatchedGovernorate).HasMaxLength(100);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(m => new { m.RequestId, m.FarmId })
            .IsUnique()
            .HasDatabaseName("IX_FarmMatch_RequestId_FarmId");

        builder.HasOne(m => m.SupplyRequest)
            .WithMany(r => r.FarmMatches)
            .HasForeignKey(m => m.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Farm)
            .WithMany(f => f.FarmMatches)
            .HasForeignKey(m => m.FarmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
