using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.HasKey(n => n.NotificationId);
        builder.Property(n => n.Title).HasMaxLength(255).IsRequired();
        builder.Property(n => n.Type).HasMaxLength(64);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(32);
        builder.HasIndex(n => n.RelatedEntityId);

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
