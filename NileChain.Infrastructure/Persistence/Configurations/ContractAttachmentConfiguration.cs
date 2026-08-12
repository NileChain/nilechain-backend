using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ContractAttachmentConfiguration : IEntityTypeConfiguration<ContractAttachment>
{
    public void Configure(EntityTypeBuilder<ContractAttachment> builder)
    {
        builder.ToTable("ContractAttachment");
        builder.HasKey(a => a.AttachmentId);
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.FileUrl).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(a => a.PublicId).HasMaxLength(260).IsRequired();
        builder.Property(a => a.Kind)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(a => a.ContractId);

        builder.HasOne(a => a.Contract)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}