using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class SigningOtpConfiguration : IEntityTypeConfiguration<SigningOtp>
{
    public void Configure(EntityTypeBuilder<SigningOtp> builder)
    {
        builder.ToTable("SigningOtp");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OtpHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsUsed).HasDefaultValue(false);

        builder.HasIndex(x => new { x.UserId, x.ContractId, x.CreatedAt })
            .HasDatabaseName("IX_SigningOtp_UserId_ContractId_CreatedAt");

        builder.HasOne(x => x.Contract)
            .WithMany(c => c.SigningOtps)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
