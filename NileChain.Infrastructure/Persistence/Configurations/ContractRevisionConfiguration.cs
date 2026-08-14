using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NileChain.Domain.Entities;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ContractRevisionConfiguration : IEntityTypeConfiguration<ContractRevision>
{
    public void Configure(EntityTypeBuilder<ContractRevision> builder)
    {
        builder.ToTable("ContractRevision");
        builder.HasKey(r => r.ContractRevisionId);

        builder.Property(r => r.PreviousText).IsRequired();
        builder.Property(r => r.NewText).IsRequired();
        builder.Property(r => r.Instructions).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.RevisedByParty)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(r => new { r.ContractId, r.CreatedAt })
            .HasDatabaseName("IX_ContractRevision_ContractId_CreatedAt");

        builder.HasOne(r => r.Contract)
            .WithMany(c => c.Revisions)
            .HasForeignKey(r => r.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
