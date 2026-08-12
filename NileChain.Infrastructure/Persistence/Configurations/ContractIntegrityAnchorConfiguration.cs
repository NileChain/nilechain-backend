using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ContractIntegrityAnchorConfiguration : IEntityTypeConfiguration<ContractIntegrityAnchor>
{
    public void Configure(EntityTypeBuilder<ContractIntegrityAnchor> builder)
    {
        builder.ToTable("ContractIntegrityAnchor");
        builder.HasKey(a => a.AnchorId);

        builder.Property(a => a.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.PreviousHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.TxRef)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(a => a.ContentHash)
            .IsUnique()
            .HasDatabaseName("IX_ContractIntegrityAnchor_ContentHash");

        builder.HasIndex(a => a.ChainIndex)
            .IsUnique()
            .HasDatabaseName("IX_ContractIntegrityAnchor_ChainIndex");

        builder.HasIndex(a => new { a.ContractId, a.Status })
            .HasDatabaseName("IX_ContractIntegrityAnchor_ContractId_Status");

        builder.HasOne(a => a.Contract)
            .WithMany(c => c.IntegrityAnchors)
            .HasForeignKey(a => a.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
