using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class TransactionEventConfiguration : IEntityTypeConfiguration<TransactionEvent>
{
    public void Configure(EntityTypeBuilder<TransactionEvent> builder)
    {
        builder.ToTable("TransactionEvents");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasIndex(e => e.TransactionId);
        builder.HasIndex(e => e.ActorUserId);

        builder.HasOne(e => e.ActorUser)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
