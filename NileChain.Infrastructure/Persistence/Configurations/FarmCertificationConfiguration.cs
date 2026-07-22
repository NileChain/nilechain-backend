using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FarmCertificationConfiguration : IEntityTypeConfiguration<FarmCertification>
{
    public void Configure(EntityTypeBuilder<FarmCertification> builder)
    {
        builder.ToTable("FarmCertification");
        builder.HasKey(fc => new { fc.FarmId, fc.CertificationId });

        builder.HasOne(fc => fc.Farm)
            .WithMany(f => f.FarmCertifications)
            .HasForeignKey(fc => fc.FarmId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fc => fc.Certification)
            .WithMany(c => c.FarmCertifications)
            .HasForeignKey(fc => fc.CertificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FarmCertification_Dates",
            "[ExpiresAt] IS NULL OR [ExpiresAt] > [IssuedAt]"));
    }
}
