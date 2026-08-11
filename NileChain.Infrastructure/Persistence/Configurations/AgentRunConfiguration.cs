using NileChain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NileChain.Infrastructure.Persistence.Configurations;

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("AgentRun");
        builder.HasKey(r => r.RunId);

        builder.Property(r => r.ErrorCode).HasMaxLength(100);
        builder.Property(r => r.OrchestratorMode).HasMaxLength(80);

        builder.HasIndex(r => r.RequestId);
        builder.HasIndex(r => r.StartedAt);

        builder.HasOne(r => r.SupplyRequest)
            .WithMany()
            .HasForeignKey(r => r.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
