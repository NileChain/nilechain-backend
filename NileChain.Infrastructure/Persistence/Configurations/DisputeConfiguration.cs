using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("Dispute");
        builder.HasKey(d => d.DisputeId);

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(d => d.RaisedByParty)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.OutcomeFavor)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(d => d.AdminNote).HasMaxLength(2000);

        builder.HasIndex(d => d.SlaDueAt)
            .HasDatabaseName("IX_Dispute_SlaDueAt");

        builder.HasIndex(d => d.ContractId)
            .HasDatabaseName("IX_Dispute_ContractId");

        builder.HasIndex(d => new { d.Status, d.Type })
            .HasDatabaseName("IX_Dispute_Status_Type");

        // At most one active (Open/UnderReview) dispute per contract.
        // Filter syntax kept provider-portable for SQL Server + SQLite test harnesses.
        builder.HasIndex(d => d.ContractId)
            .IsUnique()
            .HasFilter("Status IN ('Open', 'UnderReview')")
            .HasDatabaseName("IX_Dispute_ContractId_Active");

        builder.HasOne(d => d.Contract)
            .WithMany(c => c.Disputes)
            .HasForeignKey(d => d.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.RaisedByUser)
            .WithMany()
            .HasForeignKey(d => d.RaisedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ReviewedByUser)
            .WithMany()
            .HasForeignKey(d => d.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ResolvedByUser)
            .WithMany()
            .HasForeignKey(d => d.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Evidence)
            .WithOne(e => e.Dispute)
            .HasForeignKey(e => e.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Events)
            .WithOne(e => e.Dispute)
            .HasForeignKey(e => e.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DisputeEvidenceConfiguration : IEntityTypeConfiguration<DisputeEvidence>
{
    public void Configure(EntityTypeBuilder<DisputeEvidence> builder)
    {
        builder.ToTable("DisputeEvidence");
        builder.HasKey(e => e.DisputeEvidenceId);

        builder.Property(e => e.FileName).HasMaxLength(500).IsRequired();
        builder.Property(e => e.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(e => e.FileType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PublicId).HasMaxLength(500).IsRequired();

        builder.HasIndex(e => e.DisputeId);
    }
}

public class DisputeEventConfiguration : IEntityTypeConfiguration<DisputeEvent>
{
    public void Configure(EntityTypeBuilder<DisputeEvent> builder)
    {
        builder.ToTable("DisputeEvent");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(e => e.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.HasIndex(e => e.DisputeId);

        builder.HasOne(e => e.ActorUser)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
