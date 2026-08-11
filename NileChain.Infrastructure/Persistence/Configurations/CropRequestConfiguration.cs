using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class CropRequestConfiguration : IEntityTypeConfiguration<CropRequest>
{
    public void Configure(EntityTypeBuilder<CropRequest> builder)
    {
        builder.ToTable("CropRequest");
        builder.HasKey(c => c.CropRequestId);

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.AdminNotes).HasMaxLength(500);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(c => new { c.Name, c.Status })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'")
            .HasDatabaseName("IX_CropRequest_Name_Pending");

        builder.HasOne(c => c.RequestedByUser)
            .WithMany()
            .HasForeignKey(c => c.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ReviewedByUser)
            .WithMany()
            .HasForeignKey(c => c.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ApprovedCropType)
            .WithMany()
            .HasForeignKey(c => c.ApprovedCropTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
