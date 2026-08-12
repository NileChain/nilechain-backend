using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class EscrowTransactionConfiguration : IEntityTypeConfiguration<EscrowTransaction>
{
    public void Configure(EntityTypeBuilder<EscrowTransaction> builder)
    {
        builder.ToTable("EscrowTransactions");
        builder.HasKey(e => e.EscrowTransactionId);

        builder.Property(e => e.MilestoneAmountEgp).HasPrecision(12, 2);
        builder.Property(e => e.PlatformFeeEgp).HasPrecision(12, 2);
        builder.Property(e => e.PlatformFeePercent).HasPrecision(5, 2);
        builder.Property(e => e.TotalChargedEgp).HasPrecision(12, 2);
        builder.Property(e => e.FarmNetEgp).HasPrecision(12, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Gateway).HasMaxLength(40).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(100);
        builder.Property(e => e.PaymobOrderId).HasMaxLength(120);
        builder.Property(e => e.PaymobTransactionId).HasMaxLength(120);
        builder.Property(e => e.ReleaseReason).HasMaxLength(300);
        builder.Property(e => e.RefundReason).HasMaxLength(300);
        builder.Property(e => e.FailReason).HasMaxLength(300);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(e => e.ContractId);
        builder.HasIndex(e => e.TransactionId);
        builder.HasIndex(e => new { e.TransactionId, e.Status });
        builder.HasIndex(e => e.IdempotencyKey);

        builder.HasOne(e => e.Contract)
            .WithMany()
            .HasForeignKey(e => e.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Transaction)
            .WithMany()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
