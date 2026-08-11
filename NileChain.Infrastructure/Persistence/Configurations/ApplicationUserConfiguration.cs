using NileChain.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Required for a unique index (SQL Server disallows indexes on nvarchar(max)).
        builder.Property(u => u.PhoneNumber).HasMaxLength(32);

        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL AND [PhoneNumber] != ''")
            .HasDatabaseName("IX_AspNetUsers_PhoneNumber");
    }
}
