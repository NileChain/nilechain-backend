using NileChain.Domain.Entities;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

public class NileChainDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<Farm> Farm => Set<Farm>();
    public DbSet<FarmDocument> FarmDocuments => Set<FarmDocument>();
    public DbSet<Factory> Factory => Set<Factory>();
    public DbSet<CropType> CropTypes => Set<CropType>();
    public DbSet<CropRequest> CropRequests => Set<CropRequest>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<FarmCertification> FarmCertifications => Set<FarmCertification>();
    public DbSet<SupplyRequest> SupplyRequests => Set<SupplyRequest>();
    public DbSet<FarmMatch> FarmMatches => Set<FarmMatch>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Fulfillment> Fulfillments => Set<Fulfillment>();
    public DbSet<FulfillmentEvent> FulfillmentEvents => Set<FulfillmentEvent>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvidence> DisputeEvidence => Set<DisputeEvidence>();
    public DbSet<DisputeEvent> DisputeEvents => Set<DisputeEvent>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionEvent> TransactionEvents => Set<TransactionEvent>();
    public DbSet<ComparisonReport> ComparisonReports => Set<ComparisonReport>();
    public DbSet<RiskAssessmentReport> RiskAssessmentReports => Set<RiskAssessmentReport>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public NileChainDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NileChainDbContext).Assembly);
    }
}
