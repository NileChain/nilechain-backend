using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FactoryConfiguration : IEntityTypeConfiguration<Factory>
{
    public void Configure(EntityTypeBuilder<Factory> builder)
    {
        builder.ToTable("Factory");
        builder.HasKey(f => f.FactoryId);

        builder.Property(f => f.Name).HasMaxLength(255).IsRequired();
        builder.Property(f => f.Location).HasMaxLength(255);
        builder.Property(f => f.Governorate).HasMaxLength(100);
        builder.Property(f => f.Latitude).HasPrecision(9, 6);
        builder.Property(f => f.Longitude).HasPrecision(9, 6);
        builder.Property(f => f.IndustryType).HasMaxLength(100);
        builder.Property(f => f.AverageRating).HasPrecision(3, 2);

        builder.HasIndex(f => f.UserId).IsUnique();

        builder.HasOne(f => f.User)
            .WithOne(u => u.Factory)
            .HasForeignKey<Factory>(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
