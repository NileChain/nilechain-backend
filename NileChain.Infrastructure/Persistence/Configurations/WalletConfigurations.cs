using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");
        builder.HasKey(w => w.WalletId);
        builder.Property(w => w.AvailableBalanceEgp).HasPrecision(14, 2);
        builder.Property(w => w.HeldBalanceEgp).HasPrecision(14, 2);
        builder.Property(w => w.OwnerType).HasConversion<string>().HasMaxLength(20);
        builder.Property(w => w.RowVersion).IsRowVersion();
        builder.HasIndex(w => new { w.OwnerType, w.OwnerId }).IsUnique();
    }
}

public class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
    {
        builder.ToTable("WalletLedgerEntries");
        builder.HasKey(e => e.LedgerEntryId);
        builder.Property(e => e.AmountEgp).HasPrecision(14, 2);
        builder.Property(e => e.AvailableAfterEgp).HasPrecision(14, 2);
        builder.Property(e => e.HeldAfterEgp).HasPrecision(14, 2);
        builder.Property(e => e.Currency).HasMaxLength(8);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.ReferenceType).HasMaxLength(64);
        builder.Property(e => e.EntryType).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(e => e.WalletId);
        builder.HasOne(e => e.Wallet).WithMany(w => w.Ledger).HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WalletTopUpConfiguration : IEntityTypeConfiguration<WalletTopUp>
{
    public void Configure(EntityTypeBuilder<WalletTopUp> builder)
    {
        builder.ToTable("WalletTopUps");
        builder.HasKey(t => t.TopUpId);
        builder.Property(t => t.AmountEgp).HasPrecision(14, 2);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.IdempotencyKey).HasMaxLength(100);
        builder.Property(t => t.PaymobIntentionId).HasMaxLength(120);
        builder.Property(t => t.PaymobOrderId).HasMaxLength(120);
        builder.Property(t => t.PaymobTransactionId).HasMaxLength(120);
        builder.Property(t => t.ClientSecret).HasMaxLength(500);
        builder.Property(t => t.CheckoutUrl).HasMaxLength(2000);
        builder.HasIndex(t => t.WalletId);
        builder.HasIndex(t => t.IdempotencyKey);
        builder.HasIndex(t => t.PaymobTransactionId);
        builder.HasOne(t => t.Wallet).WithMany().HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WalletWithdrawalConfiguration : IEntityTypeConfiguration<WalletWithdrawal>
{
    public void Configure(EntityTypeBuilder<WalletWithdrawal> builder)
    {
        builder.ToTable("WalletWithdrawals");
        builder.HasKey(w => w.WithdrawalId);
        builder.Property(w => w.AmountEgp).HasPrecision(14, 2);
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(w => w.Method).HasMaxLength(40);
        builder.Property(w => w.DestinationSummary).HasMaxLength(500);
        builder.Property(w => w.FailReason).HasMaxLength(500);
        builder.HasIndex(w => w.WalletId);
        builder.HasOne(w => w.Wallet).WithMany().HasForeignKey(w => w.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
