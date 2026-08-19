using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ContractAuditLogConfiguration : IEntityTypeConfiguration<ContractAuditLog>
{
    public void Configure(EntityTypeBuilder<ContractAuditLog> builder)
    {
        builder.ToTable("ContractAuditLog", t =>
            t.HasComment("Append-only. Application must not UPDATE or DELETE rows."));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.StateHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.Timestamp).IsRequired();

        builder.HasIndex(x => new { x.ContractId, x.Timestamp })
            .HasDatabaseName("IX_ContractAuditLog_ContractId_Timestamp");

        builder.HasOne(x => x.Contract)
            .WithMany(c => c.AuditLogs)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
