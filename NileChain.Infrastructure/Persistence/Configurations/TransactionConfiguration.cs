using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.TransactionId);
        builder.Property(t => t.Amount).HasPrecision(12, 2);
        builder.Property(t => t.Percent).HasPrecision(5, 2);
        builder.Property(t => t.Label).HasMaxLength(100).IsRequired();
        builder.Property(t => t.PaymentMethod).HasMaxLength(50);
        builder.Property(t => t.ReceiptUrl).HasMaxLength(1000);
        builder.Property(t => t.ReceiptPublicId).HasMaxLength(300);
        builder.Property(t => t.ReceiptFileName).HasMaxLength(260);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(t => new { t.ContractId, t.ScheduleGeneration, t.Sequence })
            .IsUnique();

        builder.HasOne(t => t.Contract)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Events)
            .WithOne(e => e.Transaction)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
