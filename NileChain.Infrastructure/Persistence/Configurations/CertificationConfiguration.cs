using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class CertificationConfiguration : IEntityTypeConfiguration<Certification>
{
    public void Configure(EntityTypeBuilder<Certification> builder)
    {
        builder.ToTable("Certification");
        builder.HasKey(c => c.CertificationId);
        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
