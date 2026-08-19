using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NileChain.Domain.Entities;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ChannelMessageConfiguration : IEntityTypeConfiguration<ChannelMessage>
{
    public void Configure(EntityTypeBuilder<ChannelMessage> builder)
    {
        builder.ToTable("ChannelMessages");
        builder.HasKey(m => m.ChannelMessageId);

        builder.Property(m => m.Channel).HasMaxLength(32).IsRequired();
        builder.Property(m => m.ToPhone).HasMaxLength(32).IsRequired();
        builder.Property(m => m.TemplateKey).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Body).HasMaxLength(2000).IsRequired();
        builder.Property(m => m.RelatedEntityType).HasMaxLength(32);
        builder.Property(m => m.FailReason).HasMaxLength(300);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasIndex(m => m.CreatedAt);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.TemplateKey);
    }
}
