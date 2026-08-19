using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Paymob;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class PaymobMilestoneSandboxTests
{
    [Fact]
    public async Task E1_WebhookHold_ThenReleaseDeposit()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, gatewayEnabled: true);

        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(session.IsSuccess);
        Assert.Equal("Pending", session.Value!.Status);

        var browserPay = await escrow.ConfirmPaidAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(browserPay.IsFailure);
        Assert.Equal(MockEscrowErrors.WebhookRequiredConflict.Code, browserPay.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(browserPay.Error));

        var special = MockEscrowPaymentService.EscrowSpecialPrefix
                      + session.Value.EscrowTransactionId.ToString("N");
        var held = await escrow.ApplyPaymobEscrowWebhookAsync(special, "paymob-txn-1", "order-1", success: true);
        Assert.True(held.IsSuccess);
        Assert.Equal("EscrowHeld", held.Value!.Milestones.Single(m => m.Sequence == 1).Status);

        var released = await escrow.ConfirmReleaseAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(released.IsSuccess);
        Assert.Equal("Completed", released.Value!.Milestones.Single(m => m.Sequence == 1).Status);
    }

    [Fact]
    public async Task E2_DisputeWhileHeld_BlocksRelease()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, farmUserId, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, gatewayEnabled: true);
        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        var special = MockEscrowPaymentService.EscrowSpecialPrefix
                      + session.Value!.EscrowTransactionId.ToString("N");
        await escrow.ApplyPaymobEscrowWebhookAsync(special, "txn-d", null, true);

        harness.Db.Disputes.Add(new Dispute
        {
            DisputeId = Guid.NewGuid(),
            ContractId = contractId,
            Type = DisputeType.QualityShortfall,
            Status = DisputeStatus.Open,
            Description = "test",
            RaisedByParty = DisputeParty.Farm,
            RaisedByUserId = farmUserId,
            CreatedAt = DateTime.UtcNow
        });
        await harness.Db.SaveChangesAsync();

        var blocked = await escrow.ConfirmReleaseAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(blocked.IsFailure);
        Assert.Equal(PaymentMilestoneErrors.FrozenByDispute.Code, blocked.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(blocked.Error));
    }

    [Fact]
    public async Task E3_WebhookReplay_DoesNotDoubleHold()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, gatewayEnabled: true);
        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        var special = MockEscrowPaymentService.EscrowSpecialPrefix
                      + session.Value!.EscrowTransactionId.ToString("N");

        var first = await escrow.ApplyPaymobEscrowWebhookAsync(special, "same-txn", "ord", true);
        var second = await escrow.ApplyPaymobEscrowWebhookAsync(special, "same-txn", "ord", true);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var rows = await harness.Db.EscrowTransactions.AsNoTracking()
            .Where(e => e.TransactionId == deposit.TransactionId)
            .ToListAsync();
        Assert.Single(rows);
        Assert.Equal(EscrowStatus.Held, rows[0].Status);

        var events = await harness.Db.TransactionEvents.CountAsync(e =>
            e.TransactionId == deposit.TransactionId && e.ToStatus == TransactionStatus.EscrowHeld);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task E4_OfflineMarkPaid_WhileGatewayOn_IsConflict()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: true);
        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var marked = await payments.MarkPaidAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(marked.IsFailure);
        Assert.Equal(MockEscrowErrors.OfflineMarkPaidConflict.Code, marked.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(marked.Error));
    }

    [Fact]
    public async Task E5_PayBeforeFullySigned_IsConflict()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db, fullySigned: false);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, gatewayEnabled: true);
        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(session.IsFailure);
        Assert.Equal(MockEscrowErrors.ContractNotSignedConflict.Code, session.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(session.Error));
    }

    [Fact]
    public async Task E6_QcDiscountAfterHeld_SnapsAmountThenRelease()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: true);
        var escrow = CreateEscrowService(harness.Db, payments, gatewayEnabled: true);
        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var onDelivery = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 2);

        var session = await escrow.CreateSessionAsync(factoryUserId, contractId, onDelivery.TransactionId);
        var special = MockEscrowPaymentService.EscrowSpecialPrefix
                      + session.Value!.EscrowTransactionId.ToString("N");
        await escrow.ApplyPaymobEscrowWebhookAsync(special, "qc-txn", null, true);

        var original = session.Value.TotalChargedEgp;
        var qc = await escrow.ApplyQcAmountAdjustmentAsync(
            contractId, onDelivery.TransactionId, 7000m, 3500m);
        Assert.True(qc.IsSuccess);

        var row = await harness.Db.EscrowTransactions.AsNoTracking()
            .SingleAsync(e => e.EscrowTransactionId == session.Value.EscrowTransactionId);
        Assert.True(row.TotalChargedEgp < original);
        Assert.Equal(3500m, row.FarmNetEgp);

        harness.Db.Fulfillments.Add(new Fulfillment
        {
            FulfillmentId = Guid.NewGuid(),
            ContractId = contractId,
            Status = FulfillmentStatus.QualityChecked,
            CreatedAt = DateTime.UtcNow
        });
        await harness.Db.SaveChangesAsync();

        var released = await escrow.ConfirmReleaseAsync(
            factoryUserId, contractId, session.Value.EscrowTransactionId);
        Assert.True(released.IsSuccess);
        Assert.Equal("Completed", released.Value!.Milestones.Single(m => m.Sequence == 2).Status);
    }

    [Fact]
    public async Task E7_GatewayOff_LegacyMarkPaidWorks()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var (contractId, _, factoryUserId, supply) = await SeedSignedContractAsync(harness.Db);
        var payments = CreatePaymentService(harness.Db, gatewayEnabled: false, mockEnabled: false);
        await payments.EnsureCreatedForSignedContractAsync(contractId, factoryUserId, supply);
        var deposit = await harness.Db.Transactions.AsNoTracking()
            .SingleAsync(t => t.ContractId == contractId && t.Sequence == 1);

        var marked = await payments.MarkPaidAsync(factoryUserId, contractId, deposit.TransactionId);
        Assert.True(marked.IsSuccess);
        Assert.Equal("MarkedPaid", marked.Value!.Milestones.Single(m => m.Sequence == 1).Status);
    }

    [Fact]
    public void HmacReject_DoesNotVerifyTamperedPayload()
    {
        var client = new PaymobClient(
            new HttpClient(),
            Options.Create(new PaymobOptions
            {
                Enabled = true,
                SecretKey = "sk",
                PublicKey = "pk",
                HmacSecret = "hmac-secret",
                CardIntegrationId = 1
            }),
            NullLogger<PaymobClient>.Instance);

        var fields = SampleHmacFields();
        var good = ComputePaymobHmac("hmac-secret", fields);
        Assert.True(client.VerifyHmac(fields, good));
        Assert.False(client.VerifyHmac(fields, "00"));
    }

    private static PaymentMilestoneService CreatePaymentService(
        NileChainDbContext db,
        bool gatewayEnabled,
        bool mockEnabled = true) =>
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
                GatewayEnabled = gatewayEnabled,
                WalletEnabled = false,
                PlatformFeePercent = 2.5m
            }),
            NullLogger<PaymentMilestoneService>.Instance,
            new NoopCloudinary());

    private static MockEscrowPaymentService CreateEscrowService(
        NileChainDbContext db,
        PaymentMilestoneService payments,
        bool gatewayEnabled) =>
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
            new NoopWalletService(),
            Options.Create(new MockPaymentOptions
            {
                MockGatewayEnabled = true,
                GatewayEnabled = gatewayEnabled,
                WalletEnabled = false,
                PlatformFeePercent = 2.5m,
                ReleaseRequiresFactoryConfirm = true
            }),
            NullLogger<MockEscrowPaymentService>.Instance);

    private static Dictionary<string, string?> SampleHmacFields() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["amount_cents"] = "10000",
        ["created_at"] = "2026-08-17T00:00:00",
        ["currency"] = "EGP",
        ["error_occured"] = "false",
        ["has_parent_transaction"] = "false",
        ["id"] = "99",
        ["integration_id"] = "1",
        ["is_3d_secure"] = "false",
        ["is_auth"] = "false",
        ["is_capture"] = "false",
        ["is_refunded"] = "false",
        ["is_standalone_payment"] = "true",
        ["is_voided"] = "false",
        ["order"] = "1",
        ["owner"] = "1",
        ["pending"] = "false",
        ["source_data_pan"] = "1234",
        ["source_data_sub_type"] = "Visa",
        ["source_data_type"] = "card",
        ["success"] = "true"
    };

    private static string ComputePaymobHmac(string secret, IReadOnlyDictionary<string, string?> obj)
    {
        var keys = new[]
        {
            "amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction",
            "id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded",
            "is_standalone_payment", "is_voided", "order", "owner", "pending",
            "source_data_pan", "source_data_sub_type", "source_data_type", "success"
        };
        var payload = string.Concat(keys.Select(k => obj.TryGetValue(k, out var v) ? v ?? "" : ""));
        var hash = HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private class NoopWalletService : NileChain.Application.Interfaces.IWalletService
    {
        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> GetMineAsync(
            Guid userId, bool asFarm) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                WalletErrors.NotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> PaySubscriptionMonthAsync(
            Guid userId, bool asFarm) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                WalletErrors.NotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletTopUpSessionDto>> StartTopUpAsync(
            Guid userId, bool asFarm, decimal amountEgp, string? idempotencyKey, string? returnUrl) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletTopUpSessionDto>.Failure(
                WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> CompleteSimulatorTopUpAsync(
            Guid userId, bool asFarm, Guid topUpId) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> ApplyPaymobTopUpSuccessAsync(
            string specialReference, string? paymobTransactionId, string? paymobOrderId, bool success) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> ConfirmPaymobReturnAsync(
            Guid userId, bool asFarm, IReadOnlyDictionary<string, string?> query, string? hmac) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                WalletErrors.TopUpFailed));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletWithdrawalDto>> RequestWithdrawalAsync(
            Guid userId, bool asFarm, decimal amountEgp, string method, string? destinationSummary) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletWithdrawalDto>.Failure(
                WalletErrors.WithdrawFailed));

        public Task<NileChain.Application.Common.Result<Guid>> HoldForEscrowAsync(
            Guid factoryId, decimal amountEgp, Guid escrowTransactionId, string description) =>
            Task.FromResult(NileChain.Application.Common.Result<Guid>.Success(Guid.NewGuid()));

        public Task<NileChain.Application.Common.Result> EnsureFactoryAvailableAsync(
            Guid factoryId, decimal amountEgp) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public decimal GetDealHoldAmountEgp(decimal dealTotalEgp) => dealTotalEgp;

        public Task<NileChain.Application.Common.Result<Guid>> HoldDealFundsAsync(
            Guid factoryId, Guid contractId, decimal amountEgp, string description) =>
            Task.FromResult(NileChain.Application.Common.Result<Guid>.Success(Guid.NewGuid()));

        public Task<NileChain.Application.Common.Result> ReleaseEscrowToFarmAsync(
            Guid factoryId, Guid farmId, decimal totalHeldEgp, decimal farmNetEgp, Guid escrowTransactionId) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<NileChain.Application.Common.Result> RefundEscrowHoldAsync(
            Guid factoryId, decimal totalHeldEgp, Guid escrowTransactionId, string reason) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<NileChain.Application.Common.Result> RefundHeldAmountAsync(
            Guid factoryId, decimal amountEgp, string referenceType, Guid referenceId, string reason) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<NileChain.Application.Common.Result> SplitEscrowHoldAsync(
            Guid factoryId, Guid farmId, decimal totalHeldEgp, decimal farmShareEgp, Guid escrowTransactionId, string reason) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<int> ExpireStaleTopUpsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalListDto>>
            ListWithdrawalsForAdminAsync(string? status, int take = 100) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalListDto>
                .Success(new NileChain.Application.Dtos.Wallet.AdminWithdrawalListDto()));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>>
            CompleteWithdrawalAsync(Guid adminUserId, Guid withdrawalId) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>
                .Failure(WalletErrors.WithdrawalNotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>>
            RejectWithdrawalAsync(Guid adminUserId, Guid withdrawalId, string? reason) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.AdminWithdrawalDto>
                .Failure(WalletErrors.WithdrawalNotFound));
    }

    private sealed class NoopCloudinary : NileChain.Application.Interfaces.ICloudinaryService
    {
        public Task<(string Url, string PublicId)> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file) =>
            Task.FromResult(("https://example.test/receipt", "receipt-public-id"));

        public Task DeleteAsync(string publicId) => Task.CompletedTask;
    }

    private static async Task<(Guid ContractId, Guid FarmUserId, Guid FactoryUserId, SupplyRequest Supply)>
        SeedSignedContractAsync(NileChainDbContext db, bool fullySigned = true)
    {
        var farmUserId = Guid.NewGuid();
        var factoryUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        db.Users.Add(new ApplicationUser
        {
            Id = farmUserId,
            UserName = $"farm-{suffix}@test.local",
            NormalizedUserName = $"FARM-{suffix}@TEST.LOCAL",
            Email = $"farm-{suffix}@test.local",
            NormalizedEmail = $"FARM-{suffix}@TEST.LOCAL",
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        db.Users.Add(new ApplicationUser
        {
            Id = factoryUserId,
            UserName = $"factory-{suffix}@test.local",
            NormalizedUserName = $"FACTORY-{suffix}@TEST.LOCAL",
            Email = $"factory-{suffix}@test.local",
            NormalizedEmail = $"FACTORY-{suffix}@TEST.LOCAL",
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
            Status = fullySigned ? ContractStatus.Signed : ContractStatus.PendingFarmSignature,
            GeneratedText = "body",
            FarmSignedAt = fullySigned ? signedAt : null,
            FactorySignedAt = signedAt,
            SignedAt = fullySigned ? signedAt : null,
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
            var connectionString = $"DataSource=file:p1-{Guid.NewGuid():N}?mode=memory&cache=shared";
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
