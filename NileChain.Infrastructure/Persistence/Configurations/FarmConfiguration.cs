using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.ToTable("Farm");
        builder.HasKey(f => f.FarmId);

        builder.Property(f => f.Name).HasMaxLength(255).IsRequired();
        builder.Property(f => f.Location).HasMaxLength(255);
        builder.Property(f => f.Governorate).HasMaxLength(100);
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.BankName).HasMaxLength(120);
        builder.Property(f => f.AccountHolderName).HasMaxLength(120);
        builder.Property(f => f.BankAccountNumber).HasMaxLength(64);
        builder.Property(f => f.Iban).HasMaxLength(34);
        builder.Property(f => f.Latitude).HasPrecision(9, 6);
        builder.Property(f => f.Longitude).HasPrecision(9, 6);
        builder.Property(f => f.SizeInFeddans).HasPrecision(10, 2);
        builder.Property(f => f.SoilType).HasMaxLength(50).HasConversion<string>();
        builder.Property(f => f.RiskScore).HasPrecision(5, 2);
        builder.Property(f => f.AverageRating).HasPrecision(3, 2);

        builder.HasIndex(f => f.UserId).IsUnique();

        builder.HasOne(f => f.User)
            .WithOne(u => u.Farm)
            .HasForeignKey<Farm>(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
