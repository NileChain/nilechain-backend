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
        builder.Property(f => f.Carrier).HasMaxLength(120);
        builder.Property(f => f.TrackingNumber).HasMaxLength(120);
        builder.Property(f => f.ShippedNotes).HasMaxLength(1000);
        builder.Property(f => f.AcceptedQuantityTons).HasPrecision(18, 3);
        builder.Property(f => f.DiscountPercent).HasPrecision(5, 2);
        builder.Property(f => f.SpecsOutcomeNotes).HasMaxLength(2000);
        builder.Property(f => f.DeliveryPoint)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(f => f.FreightPayer)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(f => f.TransitRisk)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(f => f.WeighedQuantityTons).HasPrecision(18, 3);
        builder.Property(f => f.WeighbridgeTicketUrl).HasMaxLength(2000);
        builder.Property(f => f.GateRejectReason)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(f => f.GateRejectNotes).HasMaxLength(500);
        builder.Property(f => f.ReturnFreightBearer)
            .HasConversion<string>()
            .HasMaxLength(20);

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
