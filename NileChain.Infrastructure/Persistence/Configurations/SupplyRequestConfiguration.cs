using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class SupplyRequestConfiguration : IEntityTypeConfiguration<SupplyRequest>
{
    public void Configure(EntityTypeBuilder<SupplyRequest> builder)
    {
        builder.ToTable("SupplyRequest");
        builder.HasKey(r => r.RequestId);
        builder.Property(r => r.QuantityTons).HasPrecision(10, 2);
        builder.Property(r => r.PricePerTon).HasPrecision(10, 2);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(r => r.Factory)
            .WithMany(f => f.SupplyRequests)
            .HasForeignKey(r => r.FactoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.CropType)
            .WithMany(c => c.SupplyRequests)
            .HasForeignKey(r => r.CropTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
