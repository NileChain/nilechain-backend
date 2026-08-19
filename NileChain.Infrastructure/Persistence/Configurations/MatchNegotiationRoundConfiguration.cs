using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NileChain.Domain.Entities;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class MatchNegotiationRoundConfiguration : IEntityTypeConfiguration<MatchNegotiationRound>
{
    public void Configure(EntityTypeBuilder<MatchNegotiationRound> builder)
    {
        builder.ToTable("MatchNegotiationRound");
        builder.HasKey(r => r.RoundId);
        builder.Property(r => r.OfferedBy)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.QuantityTons).HasPrecision(10, 2);
        builder.Property(r => r.PricePerTon).HasPrecision(12, 2);
        builder.Property(r => r.Grade).HasMaxLength(80);
        builder.Property(r => r.Note).HasMaxLength(1000);

        builder.HasIndex(r => new { r.MatchId, r.CreatedAt })
            .HasDatabaseName("IX_MatchNegotiationRound_MatchId_CreatedAt");

        builder.HasOne(r => r.FarmMatch)
            .WithMany(m => m.NegotiationRounds)
            .HasForeignKey(r => r.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
