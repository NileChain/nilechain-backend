using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ContractSignatureRecordConfiguration : IEntityTypeConfiguration<ContractSignatureRecord>
{
    public void Configure(EntityTypeBuilder<ContractSignatureRecord> builder)
    {
        builder.ToTable("ContractSignatureRecord");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContractHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SignatureToken)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ConsentText)
            .IsRequired();

        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.SignedAt).IsRequired();

        builder.HasIndex(x => new { x.ContractId, x.SignerId })
            .IsUnique()
            .HasDatabaseName("IX_ContractSignatureRecord_ContractId_SignerId");

        builder.HasOne(x => x.Contract)
            .WithMany(c => c.SignatureRecords)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Signer)
            .WithMany()
            .HasForeignKey(x => x.SignerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
