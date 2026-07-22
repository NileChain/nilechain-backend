using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class CropTypeConfiguration : IEntityTypeConfiguration<CropType>
{
    public void Configure(EntityTypeBuilder<CropType> builder)
    {
        builder.ToTable("CropType");
        builder.HasKey(c => c.CropTypeId);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
