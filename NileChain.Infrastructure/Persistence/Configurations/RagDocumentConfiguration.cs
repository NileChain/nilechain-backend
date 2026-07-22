using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class RagDocumentConfiguration : IEntityTypeConfiguration<RagDocument>
{
    public void Configure(EntityTypeBuilder<RagDocument> builder)
    {
        builder.ToTable("RagDocument");
        builder.HasKey(d => d.DocumentId);
        builder.Property(d => d.Title).HasMaxLength(255).IsRequired();
        builder.Property(d => d.Category).HasMaxLength(30);
        builder.Property(d => d.FilePath).HasMaxLength(500).IsRequired();

        builder.HasOne(d => d.Uploader)
            .WithMany()
            .HasForeignKey(d => d.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
