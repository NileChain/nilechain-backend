using NileChain.Domain.Entities;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence;

public class NileChainDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<Farm> Farm => Set<Farm>();
    public DbSet<FarmDocument> FarmDocuments => Set<FarmDocument>();
    public DbSet<FarmImage> FarmImages => Set<FarmImage>();
    public DbSet<Factory> Factory => Set<Factory>();
    public DbSet<FactoryDocument> FactoryDocuments => Set<FactoryDocument>();
    public DbSet<CropType> CropTypes => Set<CropType>();
    public DbSet<FarmCrop> FarmCrops => Set<FarmCrop>();
    public DbSet<CropRequest> CropRequests => Set<CropRequest>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<FarmCertification> FarmCertifications => Set<FarmCertification>();
    public DbSet<SupplyRequest> SupplyRequests => Set<SupplyRequest>();
    public DbSet<FarmMatch> FarmMatches => Set<FarmMatch>();
    public DbSet<MatchNegotiationRound> MatchNegotiationRounds => Set<MatchNegotiationRound>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractRevision> ContractRevisions => Set<ContractRevision>();
    public DbSet<ContractAttachment> ContractAttachments => Set<ContractAttachment>();
    public DbSet<ContractIntegrityAnchor> ContractIntegrityAnchors => Set<ContractIntegrityAnchor>();
    public DbSet<Fulfillment> Fulfillments => Set<Fulfillment>();
    public DbSet<FulfillmentEvent> FulfillmentEvents => Set<FulfillmentEvent>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvidence> DisputeEvidence => Set<DisputeEvidence>();
    public DbSet<DisputeEvent> DisputeEvents => Set<DisputeEvent>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionEvent> TransactionEvents => Set<TransactionEvent>();
    public DbSet<EscrowTransaction> EscrowTransactions => Set<EscrowTransaction>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();
    public DbSet<WalletTopUp> WalletTopUps => Set<WalletTopUp>();
    public DbSet<WalletWithdrawal> WalletWithdrawals => Set<WalletWithdrawal>();
    public DbSet<ComparisonReport> ComparisonReports => Set<ComparisonReport>();
    public DbSet<RiskAssessmentReport> RiskAssessmentReports => Set<RiskAssessmentReport>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<SigningOtp> SigningOtps => Set<SigningOtp>();
    public DbSet<ContractSignatureRecord> ContractSignatureRecords => Set<ContractSignatureRecord>();
    public DbSet<ContractAuditLog> ContractAuditLogs => Set<ContractAuditLog>();
    public DbSet<ChannelMessage> ChannelMessages => Set<ChannelMessage>();
    public DbSet<KybVerificationReport> KybVerificationReports => Set<KybVerificationReport>();
    public DbSet<KybDecision> KybDecisions => Set<KybDecision>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionUsage> SubscriptionUsages => Set<SubscriptionUsage>();

    public NileChainDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NileChainDbContext).Assembly);
    }
}
