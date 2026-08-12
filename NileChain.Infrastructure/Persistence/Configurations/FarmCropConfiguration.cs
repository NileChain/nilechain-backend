using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FarmCropConfiguration : IEntityTypeConfiguration<FarmCrop>
{
    public void Configure(EntityTypeBuilder<FarmCrop> builder)
    {
        builder.ToTable("FarmCrop");
        builder.HasKey(fc => new { fc.FarmId, fc.CropTypeId });

        builder.Property(fc => fc.AvailableQuantityTons).HasPrecision(10, 2);
        builder.Property(fc => fc.MinPricePerTon).HasPrecision(12, 2);
        builder.Property(fc => fc.IsPublished).HasDefaultValue(true);

        builder.HasOne(fc => fc.Farm)
            .WithMany(f => f.FarmCrops)
            .HasForeignKey(fc => fc.FarmId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fc => fc.CropType)
            .WithMany(c => c.FarmCrops)
            .HasForeignKey(fc => fc.CropTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FarmCrop_AvailabilityDates",
            "[AvailableTo] IS NULL OR [AvailableFrom] IS NULL OR [AvailableTo] >= [AvailableFrom]"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FarmCrop_AvailableQuantityNonNegative",
            "[AvailableQuantityTons] IS NULL OR [AvailableQuantityTons] >= 0"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FarmCrop_MinPriceNonNegative",
            "[MinPricePerTon] IS NULL OR [MinPricePerTon] >= 0"));
    }
}
