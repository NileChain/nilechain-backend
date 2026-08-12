using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FarmImageConfiguration : IEntityTypeConfiguration<FarmImage>
{
    public void Configure(EntityTypeBuilder<FarmImage> builder)
    {
        builder.ToTable("FarmImage");
        builder.HasKey(i => i.FarmImageId);

        builder.Property(i => i.FileName).HasMaxLength(255).IsRequired();
        builder.Property(i => i.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(i => i.PublicId).HasMaxLength(512).IsRequired();

        builder.HasOne(i => i.Farm)
            .WithMany(f => f.FarmImages)
            .HasForeignKey(i => i.FarmId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.FarmId, i.SortOrder });
    }
}
