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
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<FarmCertification> FarmCertifications => Set<FarmCertification>();
    public DbSet<SupplyRequest> SupplyRequests => Set<SupplyRequest>();
    public DbSet<FarmMatch> FarmMatches => Set<FarmMatch>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ComparisonReport> ComparisonReports => Set<ComparisonReport>();
    public DbSet<RiskAssessmentReport> RiskAssessmentReports => Set<RiskAssessmentReport>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public NileChainDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NileChainDbContext).Assembly);
    }
}
