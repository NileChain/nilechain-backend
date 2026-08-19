using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Errors;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class MockEscrowPaymentServiceTests
{
    [Fact]
    public async Task MockPay_Deposit_HoldsWithPlatformFee_ThenRelease()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, mockEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments);

        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var markBlocked = await payments.MarkPaidAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(markBlocked.IsFailure);
        Assert.Equal(MockEscrowErrors.UseMockPay.Code, markBlocked.Error!.Code);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(session.IsSuccess);
        Assert.Equal(3000m, session.Value!.MilestoneAmountEgp);
        Assert.Equal(75m, session.Value.PlatformFeeEgp); // 2.5% of 3000
        Assert.Equal(3075m, session.Value.TotalChargedEgp);
        Assert.Equal(3000m, session.Value.FarmNetEgp);

        var held = await escrow.ConfirmPaidAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(held.IsSuccess);
        Assert.Equal("EscrowHeld", held.Value!.Milestones.Single(m => m.Sequence == 1).Status);

        var released = await escrow.ConfirmReleaseAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(released.IsSuccess);
        Assert.Equal("Completed", released.Value!.Milestones.Single(m => m.Sequence == 1).Status);

        var row = await harness.Db.EscrowTransactions.AsNoTracking()
            .SingleAsync(e => e.EscrowTransactionId == session.Value.EscrowTransactionId);
        Assert.Equal(EscrowStatus.Released, row.Status);
        Assert.Equal(75m, row.PlatformFeeEgp);
    }

    [Fact]
    public async Task OnDelivery_Release_RequiresFulfillmentReceived()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, mockEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments);

        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var onDelivery = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 2);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, onDelivery.TransactionId);
        Assert.True(session.IsSuccess);
        await escrow.ConfirmPaidAsync(factoryUserId, contractId, session.Value!.EscrowTransactionId);

        var blocked = await escrow.ConfirmReleaseAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(blocked.IsFailure);
        Assert.Equal(MockEscrowErrors.ReleaseNotReady.Code, blocked.Error!.Code);

        harness.Db.Fulfillments.Add(new Fulfillment
        {
            FulfillmentId = Guid.NewGuid(),
            ContractId = contractId,
            Status = FulfillmentStatus.Received,
            CreatedAt = DateTime.UtcNow
        });
        await harness.Db.SaveChangesAsync();

        var released = await escrow.ConfirmReleaseAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(released.IsSuccess);
        Assert.Equal("Completed", released.Value!.Milestones.Single(m => m.Sequence == 2).Status);
    }

    [Fact]
    public async Task TwoMilestones_WithDealHoldIncludingFees_BothRelease()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);

        var hold = NileChain.Application.Contracts.PlatformFeeMath.TotalCharged(10000m, 2.5m);
        var contract = await harness.Db.Contracts.SingleAsync(c => c.ContractId == contractId);
        contract.FundsHeldAt = DateTime.UtcNow;
        contract.FundsHeldEgp = hold;
        await harness.Db.SaveChangesAsync();

        var ledger = new LedgerWalletService { Held = hold, Available = 0 };
        var payments = CreatePaymentService(harness.Db, mockEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, ledger, walletEnabled: true);

        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);
        var onDelivery = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 2);

        var s1 = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(s1.IsSuccess);
        var held1 = await escrow.ConfirmPaidAsync(factoryUserId, contractId, s1.Value!.EscrowTransactionId);
        Assert.True(held1.IsSuccess);
        var rel1 = await escrow.ConfirmReleaseAsync(factoryUserId, contractId, s1.Value.EscrowTransactionId);
        Assert.True(rel1.IsSuccess);

        var s2 = await escrow.CreateSessionAsync(factoryUserId, contractId, onDelivery.TransactionId);
        Assert.True(s2.IsSuccess);
        var held2 = await escrow.ConfirmPaidAsync(factoryUserId, contractId, s2.Value!.EscrowTransactionId);
        Assert.True(held2.IsSuccess);

        harness.Db.Fulfillments.Add(new Fulfillment
        {
            FulfillmentId = Guid.NewGuid(),
            ContractId = contractId,
            Status = FulfillmentStatus.Received,
            CreatedAt = DateTime.UtcNow
        });
        await harness.Db.SaveChangesAsync();

        var rel2 = await escrow.ConfirmReleaseAsync(factoryUserId, contractId, s2.Value.EscrowTransactionId);
        Assert.True(rel2.IsSuccess, rel2.Error?.Description);
        Assert.Equal(0m, ledger.Held);
        Assert.Equal(250m, ledger.PlatformAvailable);
        Assert.Equal(10000m, ledger.FarmAvailable);
    }

    [Fact]
    public async Task ConfirmPaid_WhenDealFundsAlreadyHeld_SkipsAvailableHold()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);

        var contract = await harness.Db.Contracts.SingleAsync(c => c.ContractId == contractId);
        contract.FundsHeldAt = DateTime.UtcNow;
        contract.FundsHeldEgp = 10000m;
        await harness.Db.SaveChangesAsync();

        var tracker = new TrackingWalletService();
        var payments = CreatePaymentService(harness.Db, mockEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, tracker, walletEnabled: true);

        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(session.IsSuccess);

        var held = await escrow.ConfirmPaidAsync(
            factoryUserId, contractId, session.Value!.EscrowTransactionId);
        Assert.True(held.IsSuccess);
        Assert.Equal(0, tracker.HoldForEscrowCalls);
        Assert.Equal("EscrowHeld", held.Value!.Milestones.Single(m => m.Sequence == 1).Status);
    }

    private static PaymentMilestoneService CreatePaymentService(NileChainDbContext db, bool mockEnabled) =>
        new(
            new PaymentMilestoneRepository(db),
            new EscrowTransactionRepository(db),
            new DisputeRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Contract>(db),
            new Repository<Notification>(db),
            new UnitOfWork(db),
            Options.Create(new PaymentMilestoneOptions()),
            Options.Create(new MockPaymentOptions
            {
                MockGatewayEnabled = mockEnabled,
                PlatformFeePercent = 2.5m
            }),
            NullLogger<PaymentMilestoneService>.Instance,
            new NoopCloudinary());

    private static MockEscrowPaymentService CreateEscrowService(
        NileChainDbContext db,
        PaymentMilestoneService payments,
        NileChain.Application.Interfaces.IWalletService? wallets = null,
        bool walletEnabled = false) =>
        new(
            new EscrowTransactionRepository(db),
            new PaymentMilestoneRepository(db),
            new DisputeRepository(db),
            new FulfillmentRepository(db),
            new FarmRepository(db),
            new FactoryRepository(db),
            new Repository<Contract>(db),
            new Repository<Notification>(db),
            new UnitOfWork(db),
            payments,
            wallets ?? new NoopWalletService(),
            Options.Create(new MockPaymentOptions
            {
                MockGatewayEnabled = true,
                WalletEnabled = walletEnabled,
                PlatformFeePercent = 2.5m
            }),
            NullLogger<MockEscrowPaymentService>.Instance);

    private sealed class TrackingWalletService : NoopWalletService
    {
        public int HoldForEscrowCalls { get; private set; }

        public override Task<NileChain.Application.Common.Result<Guid>> HoldForEscrowAsync(
            Guid factoryId, decimal amountEgp, Guid escrowTransactionId, string description)
        {
            HoldForEscrowCalls++;
            return Task.FromResult(NileChain.Application.Common.Result<Guid>.Success(Guid.NewGuid()));
        }
    }

    private sealed class LedgerWalletService : NoopWalletService
    {
        public decimal Available { get; set; }
        public decimal Held { get; set; }
        public decimal FarmAvailable { get; set; }
        public decimal PlatformAvailable { get; set; }

        public override Task<NileChain.Application.Common.Result<Guid>> HoldForEscrowAsync(
            Guid factoryId, decimal amountEgp, Guid escrowTransactionId, string description)
        {
            if (Available < amountEgp)
                return Task.FromResult(NileChain.Application.Common.Result<Guid>.Failure(
                    NileChain.Application.Errors.WalletErrors.InsufficientBalance));
            Available -= amountEgp;
            Held += amountEgp;
            return Task.FromResult(NileChain.Application.Common.Result<Guid>.Success(Guid.NewGuid()));
        }

        public override Task<NileChain.Application.Common.Result> ReleaseEscrowToFarmAsync(
            Guid factoryId, Guid farmId, decimal totalHeldEgp, decimal farmNetEgp, Guid escrowTransactionId)
        {
            if (Held < totalHeldEgp)
                return Task.FromResult(NileChain.Application.Common.Result.Failure(
                    NileChain.Application.Errors.WalletErrors.InsufficientBalance));
            Held -= totalHeldEgp;
            FarmAvailable += farmNetEgp;
            var fee = totalHeldEgp - farmNetEgp;
            if (fee > 0)
                PlatformAvailable += fee;
            return Task.FromResult(NileChain.Application.Common.Result.Success());
        }
    }

    private class NoopWalletService : NileChain.Application.Interfaces.IWalletService
    {
        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> GetMineAsync(
            Guid userId, bool asFarm) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.NotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> PaySubscriptionMonthAsync(
            Guid userId, bool asFarm) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.NotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletTopUpSessionDto>> StartTopUpAsync(
            Guid userId, bool asFarm, decimal amountEgp, string? idempotencyKey, string? returnUrl) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletTopUpSessionDto>.Failure(
                NileChain.Application.Errors.WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> CompleteSimulatorTopUpAsync(
            Guid userId, bool asFarm, Guid topUpId) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> ApplyPaymobTopUpSuccessAsync(
            string specialReference, string? paymobTransactionId, string? paymobOrderId, bool success) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> ConfirmPaymobReturnAsync(
            Guid userId,
            bool asFarm,
            IReadOnlyDictionary<string, string?> query,
            string? hmac) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletWithdrawalDto>> RequestWithdrawalAsync(
            Guid userId, bool asFarm, decimal amountEgp, string method, string? destinationSummary) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletWithdrawalDto>.Failure(
                NileChain.Application.Errors.WalletErrors.WithdrawFailed));

        public virtual Task<NileChain.Application.Common.Result<Guid>> HoldForEscrowAsync(
            Guid factoryId, decimal amountEgp, Guid escrowTransactionId, string description) =>
            Task.FromResult(NileChain.Application.Common.Result<Guid>.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public virtual Task<NileChain.Application.Common.Result> ReleaseEscrowToFarmAsync(
            Guid factoryId, Guid farmId, decimal totalHeldEgp, decimal farmNetEgp, Guid escrowTransactionId) =>
            Task.FromResult(NileChain.Application.Common.Result.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public Task<NileChain.Application.Common.Result> EnsureFactoryAvailableAsync(
            Guid factoryId, decimal amountEgp) =>
            Task.FromResult(NileChain.Application.Common.Result.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public Task<NileChain.Application.Common.Result<Guid>> HoldDealFundsAsync(
            Guid factoryId, Guid contractId, decimal amountEgp, string description) =>
            Task.FromResult(NileChain.Application.Common.Result<Guid>.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public Task<NileChain.Application.Common.Result> RefundEscrowHoldAsync(
            Guid factoryId, decimal totalHeldEgp, Guid escrowTransactionId, string reason) =>
            Task.FromResult(NileChain.Application.Common.Result.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public decimal GetDealHoldAmountEgp(decimal dealTotalEgp) => dealTotalEgp;

        public Task<NileChain.Application.Common.Result> RefundHeldAmountAsync(
            Guid factoryId, decimal amountEgp, string referenceType, Guid referenceId, string reason) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<NileChain.Application.Common.Result> SplitEscrowHoldAsync(
            Guid factoryId,
            Guid farmId,
            decimal totalHeldEgp,
            decimal farmShareEgp,
            Guid escrowTransactionId,
            string reason) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<int> ExpireStaleTopUpsAsync(
            DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalListDto>>
            ListWithdrawalsForAdminAsync(string? status, int take = 100) =>
            Task.FromResult(
                NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalListDto>
                    .Success(new NileChain.Application.Dtos.Wallet.AdminWithdrawalListDto()));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>>
            CompleteWithdrawalAsync(Guid adminUserId, Guid withdrawalId) =>
            Task.FromResult(
                NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>
                    .Failure(NileChain.Application.Errors.WalletErrors.WithdrawalNotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>>
            RejectWithdrawalAsync(Guid adminUserId, Guid withdrawalId, string? reason) =>
            Task.FromResult(
                NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>
                    .Failure(NileChain.Application.Errors.WalletErrors.WithdrawalNotFound));
    }

    private sealed class NoopCloudinary : NileChain.Application.Interfaces.ICloudinaryService
    {
        public Task<(string Url, string PublicId)> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file) =>
            Task.FromResult(("https://example.test/receipt", "receipt-public-id"));

        public Task DeleteAsync(string publicId) => Task.CompletedTask;
    }

    private static async Task<(Guid ContractId, Guid FarmUserId, Guid FactoryUserId, SupplyRequest Supply)>
        SeedSignedContractAsync(NileChainDbContext db)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();

        db.Users.Add(new ApplicationUser
        {
            Id = farmUserId,
            UserName = "farm-escrow@test.local",
            NormalizedUserName = "FARM-ESCROW@TEST.LOCAL",
            Email = "farm-escrow@test.local",
            NormalizedEmail = "FARM-ESCROW@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        db.Users.Add(new ApplicationUser
        {
            Id = factoryUserId,
            UserName = "factory-escrow@test.local",
            NormalizedUserName = "FACTORY-ESCROW@TEST.LOCAL",
            Email = "factory-escrow@test.local",
            NormalizedEmail = "FACTORY-ESCROW@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });

        var farmId = Guid.NewGuid();
        var factoryId = Guid.NewGuid();
        db.Farm.Add(new Farm
        {
            FarmId = farmId,
            UserId = farmUserId,
            Name = "Escrow Farm",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });
        db.Factory.Add(new Factory
        {
            FactoryId = factoryId,
            UserId = factoryUserId,
            Name = "Escrow Factory",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        });

        var cropId = Guid.NewGuid();
        db.CropTypes.Add(new CropType { CropTypeId = cropId, Name = "Wheat" });

        var requestId = Guid.NewGuid();
        var supply = new SupplyRequest
        {
            RequestId = requestId,
            FactoryId = factoryId,
            CropTypeId = cropId,
            QuantityTons = 10,
            PricePerTon = 1000m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(10),
            Status = SupplyRequestStatus.Matched,
            CreatedAt = DateTime.UtcNow
        };
        db.SupplyRequests.Add(supply);

        var matchId = Guid.NewGuid();
        db.FarmMatches.Add(new FarmMatch
        {
            MatchId = matchId,
            RequestId = requestId,
            FarmId = farmId,
            Status = FarmMatchStatus.Accepted,
            MatchScore = 80,
            CreatedAt = DateTime.UtcNow
        });

        var contractId = Guid.NewGuid();
        var signedAt = DateTime.UtcNow;
        db.Contracts.Add(new Contract
        {
            ContractId = contractId,
            MatchId = matchId,
            Status = ContractStatus.Signed,
            GeneratedText = "body",
            FarmSignedAt = signedAt,
            FactorySignedAt = signedAt,
            SignedAt = signedAt,
            CreatedAt = signedAt,
            RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 1 }
        });

        await db.SaveChangesAsync();
        return (contractId, farmUserId, factoryUserId, supply);
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;

        private SqliteHarness(SqliteConnection keepAlive, NileChainDbContext db)
        {
            _keepAlive = keepAlive;
            Db = db;
        }

        public NileChainDbContext Db { get; }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connectionString = $"DataSource=file:escrow-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();

            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var db = new SqliteLifecycleDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class SqliteLifecycleDbContext : NileChainDbContext
    {
        public SqliteLifecycleDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<FarmMatch>()
                .Property(m => m.EligibilitySnapshotJson)
                .HasColumnType("TEXT");
        }
    }
}
