using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contract");
        builder.HasKey(c => c.ContractId);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(c => c.MatchId).IsUnique();

        builder.HasOne(c => c.FarmMatch)
            .WithOne(fm => fm.Contract)
            .HasForeignKey<Contract>(c => c.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.RagDocuments)
            .WithMany(d => d.Contracts)
            .UsingEntity(j => j.ToTable("ContractRagDocument"));
    }
}
