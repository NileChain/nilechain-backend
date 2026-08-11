using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class FulfillmentConfiguration : IEntityTypeConfiguration<Fulfillment>
{
    public void Configure(EntityTypeBuilder<Fulfillment> builder)
    {
        builder.ToTable("Fulfillment");
        builder.HasKey(f => f.FulfillmentId);

        builder.Property(f => f.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(f => f.QualityNotes).HasMaxLength(2000);

        builder.HasIndex(f => f.ContractId)
            .IsUnique()
            .HasDatabaseName("IX_Fulfillment_ContractId");

        builder.HasIndex(f => new { f.Status, f.PlannedShipDate })
            .HasDatabaseName("IX_Fulfillment_Status_PlannedShipDate");

        builder.HasOne(f => f.Contract)
            .WithOne(c => c.Fulfillment)
            .HasForeignKey<Fulfillment>(f => f.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Events)
            .WithOne(e => e.Fulfillment)
            .HasForeignKey(e => e.FulfillmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FulfillmentEventConfiguration : IEntityTypeConfiguration<FulfillmentEvent>
{
    public void Configure(EntityTypeBuilder<FulfillmentEvent> builder)
    {
        builder.ToTable("FulfillmentEvent");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(e => e.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.HasIndex(e => e.FulfillmentId);

        builder.HasOne(e => e.ActorUser)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
