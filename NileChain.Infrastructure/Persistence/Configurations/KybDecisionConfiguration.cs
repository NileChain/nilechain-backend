using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class KybDecisionConfiguration : IEntityTypeConfiguration<KybDecision>
{
    public void Configure(EntityTypeBuilder<KybDecision> builder)
    {
        builder.ToTable("KybDecision");
        builder.HasKey(d => d.DecisionId);

        builder.Property(d => d.Action)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Reason).HasMaxLength(2000).IsRequired();

        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => d.CreatedAt);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(d => d.AdminUser)
            .WithMany()
            .HasForeignKey(d => d.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
