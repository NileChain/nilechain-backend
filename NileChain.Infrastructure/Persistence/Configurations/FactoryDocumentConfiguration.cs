using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NileChain.Domain.Entities;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FactoryDocumentConfiguration : IEntityTypeConfiguration<FactoryDocument>
{
    public void Configure(EntityTypeBuilder<FactoryDocument> builder)
    {
        builder.ToTable("FactoryDocument");
        builder.HasKey(d => d.FactoryDocumentId);

        builder.Property(d => d.FileName).HasMaxLength(500).IsRequired();
        builder.Property(d => d.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.FileType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.PublicId).HasMaxLength(500).IsRequired();
        builder.Property(d => d.KybKind)
            .HasConversion<string>()
            .HasMaxLength(40)
            .HasDefaultValue(NileChain.Domain.Enums.KybKind.Other)
            .IsRequired();

        builder.HasOne(d => d.Factory)
            .WithMany(f => f.FactoryDocuments)
            .HasForeignKey(d => d.FactoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
