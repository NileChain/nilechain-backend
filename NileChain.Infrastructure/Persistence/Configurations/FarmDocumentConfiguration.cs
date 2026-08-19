using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FarmDocumentConfiguration : IEntityTypeConfiguration<FarmDocument>
{
    public void Configure(EntityTypeBuilder<FarmDocument> builder)
    {
        builder.ToTable("FarmDocument");
        builder.HasKey(d => d.FarmDocumentId);

        builder.Property(d => d.FileName).HasMaxLength(500).IsRequired();
        builder.Property(d => d.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.FileType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.PublicId).HasMaxLength(500).IsRequired();
        builder.Property(d => d.KybKind)
            .HasConversion<string>()
            .HasMaxLength(40)
            .HasDefaultValue(NileChain.Domain.Enums.KybKind.Other)
            .IsRequired();

        builder.HasOne(d => d.Farm)
            .WithMany(f => f.FarmDocuments)
            .HasForeignKey(d => d.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
