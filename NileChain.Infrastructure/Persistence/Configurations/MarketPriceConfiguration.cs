using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class MarketPriceConfiguration : IEntityTypeConfiguration<MarketPrice>
{
    public void Configure(EntityTypeBuilder<MarketPrice> builder)
    {
        builder.ToTable("MarketPrice");
        builder.HasKey(p => p.PriceId);
        builder.Property(p => p.Governorate).HasMaxLength(100);
        builder.Property(p => p.PricePerTon).HasPrecision(10, 2);
        builder.Property(p => p.Source).HasMaxLength(50);

        builder.HasOne(p => p.CropType)
            .WithMany(c => c.MarketPrices)
            .HasForeignKey(p => p.CropTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
