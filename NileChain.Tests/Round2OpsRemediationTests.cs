using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Resilience;
using NileChain.API.Middleware;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Services;
using NileChain.Application.Validation;
using NileChain.Domain.Entities;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class Round2OpsRemediationTests
{
    [Fact]
    public void FileUploadValidation_RejectsDisallowedExtensionAndOversized()
    {
        var pdfHeader = Encoding.ASCII.GetBytes("%PDF-1.4");
        var ok = FileUploadValidation.Validate("doc.pdf", "application/pdf", 100, pdfHeader);
        Assert.True(ok.IsValid);

        var badExt = FileUploadValidation.Validate("malware.exe", "application/octet-stream", 100, pdfHeader);
        Assert.False(badExt.IsValid);
        Assert.Equal("File.TypeNotAllowed", badExt.ErrorCode);

        var tooBig = FileUploadValidation.Validate(
            "doc.pdf",
            "application/pdf",
            FileUploadValidation.MaxBytes + 1,
            pdfHeader);
        Assert.False(tooBig.IsValid);
        Assert.Equal("File.TooLarge", tooBig.ErrorCode);
    }

    [Fact]
    public void FileUploadValidation_RejectsMagicMismatch()
    {
        var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var mismatch = FileUploadValidation.Validate("photo.png", "image/png", 100, jpegHeader);
        Assert.False(mismatch.IsValid);
        Assert.Equal("File.ContentMismatch", mismatch.ErrorCode);

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.True(FileUploadValidation.Validate("a.png", "image/png", 100, png).IsValid);

        var webp = Encoding.ASCII.GetBytes("RIFF....WEBP");
        Assert.True(FileUploadValidation.Validate("a.webp", "image/webp", 100, webp).IsValid);
    }

    [Fact]
    public void RagUploadValidation_AllowlistSizeAndBinaryRejection()
    {
        var text = Encoding.UTF8.GetBytes("hello rag");
        var ok = RagUploadValidation.Validate("notes.md", text.Length, text, contentContainsNul: false);
        Assert.True(ok.IsValid);
        Assert.EndsWith(".md", ok.SafeStoredFileName);
        Assert.DoesNotContain("notes", ok.SafeStoredFileName!);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7");
        Assert.True(RagUploadValidation.Validate("x.pdf", pdf.Length, pdf).IsValid);

        var exe = Encoding.ASCII.GetBytes("MZ....");
        Assert.False(RagUploadValidation.Validate("x.exe", exe.Length, exe).IsValid);

        var tooBig = RagUploadValidation.Validate(
            "notes.txt",
            RagUploadValidation.MaxBytes + 1,
            text);
        Assert.Equal("Rag.FileTooLarge", tooBig.ErrorCode);

        var binaryText = RagUploadValidation.Validate("notes.txt", 10, text, contentContainsNul: true);
        Assert.Equal("Rag.BinaryRejected", binaryText.ErrorCode);
    }

    [Fact]
    public void ResultHttpMapper_MapsStatusCodes()
    {
        Assert.Equal(HttpStatusCode.NotFound, ResultHttpMapper.MapStatus(FarmErrors.FarmNotFound));
        Assert.Equal(HttpStatusCode.Forbidden, ResultHttpMapper.MapStatus(FactoryErrors.UnauthorizedAccess));
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(AuthErrors.EmailAlreadyExists));
        Assert.Equal(HttpStatusCode.Conflict, ResultHttpMapper.MapStatus(FactoryErrors.ConcurrencyConflict));
        Assert.Equal(HttpStatusCode.BadRequest, ResultHttpMapper.MapStatus(new Error("Validation.Error", "bad")));

        var body = ResultHttpMapper.ToTypedBody(FarmErrors.FarmNotFound);
        Assert.Equal("Farm.NotFound", body.Code);
        Assert.Equal("Farm not found.", body.Message);
    }

    [Fact]
    public async Task ExceptionMiddleware_ReturnsCorrelationIdAndLogs()
    {
        var middleware = new ExceptionMiddleware(_ => throw new InvalidOperationException("secret-leak"));
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-abc-123";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, NullLogger<ExceptionMiddleware>.Instance);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Server.InternalError", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal("trace-abc-123", doc.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("secret-leak", json);
    }

    [Fact]
    public void LlmCircuitBreaker_OpensAfterFailuresAndAllowsHalfOpenProbe()
    {
        var breaker = new LlmCircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(50));
        Assert.True(breaker.TryEnter(out _));
        breaker.RecordFailure();
        Assert.True(breaker.TryEnter(out _));
        breaker.RecordFailure();
        Assert.True(breaker.IsOpen);
        Assert.False(breaker.TryEnter(out var reason));
        Assert.Equal(LlmCircuitBreaker.ClientSafeUnavailable, reason);

        Thread.Sleep(60);
        Assert.True(breaker.TryEnter(out _)); // half-open probe
        breaker.RecordSuccess();
        Assert.False(breaker.IsOpen);
        Assert.True(breaker.TryEnter(out _));
    }

    [Fact]
    public void Contract_HasRowVersionConcurrencyToken()
    {
        var prop = typeof(Contract).GetProperty(nameof(Contract.RowVersion));
        Assert.NotNull(prop);
        Assert.Equal(typeof(byte[]), prop!.PropertyType);

        var configPath = Path.Combine(
            FindBackendRoot(),
            "NileChain.Infrastructure",
            "Persistence",
            "Configurations",
            "ContractConfiguration.cs");
        var text = File.ReadAllText(configPath);
        Assert.Contains("IsRowVersion", text);
        Assert.Contains("RowVersion", text);
    }

    [Fact]
    public void SupplyRequest_HasIdempotencyKeyAndUniqueIndexMigration()
    {
        Assert.NotNull(typeof(SupplyRequest).GetProperty(nameof(SupplyRequest.IdempotencyKey)));

        var migrationPath = Path.Combine(
            FindBackendRoot(),
            "NileChain.Infrastructure",
            "Persistence",
            "Migrations",
            "20260811123000_AddAgentRunIdempotencyRowVersion.cs");
        var text = File.ReadAllText(migrationPath);
        Assert.Contains("IdempotencyKey", text);
        Assert.Contains("IX_SupplyRequest_FactoryId_IdempotencyKey", text);
        Assert.Contains("RowVersion", text);
        Assert.Contains("AgentRun", text);
    }

    [Fact]
    public void ClientErrorSanitizer_StripsExceptionLeaksFromTrail()
    {
        var leaked = ClientErrorSanitizer.SanitizeTrailText("ERROR: NullReferenceException: Object ref");
        Assert.Equal("An internal error occurred during this step.", leaked);
        Assert.DoesNotContain("NullReference", ClientErrorSanitizer.AgentFailureMessage);
    }

    [Fact]
    public void ChromaLookupResult_UnavailableSurfacesSafeMessage()
    {
        var unavailable = NileChain.AI.RAG.ChromaLookupResult.Unavailable();
        Assert.False(unavailable.IsAvailable);
        Assert.Equal(ClientErrorSanitizer.ServiceUnavailableMessage, unavailable.Content);
    }

    [Fact]
    public async Task IdempotencyKey_Reuse_ReturnsSameRequestId()
    {
        await using var db = new NileChainDbContext(
            new DbContextOptionsBuilder<NileChainDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var userId = Guid.NewGuid();
        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = userId,
            Name = "Idem Factory",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        };
        var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = "Wheat" };
        db.Factory.Add(factory);
        db.CropTypes.Add(crop);
        await db.SaveChangesAsync();

        var factoryRepo = new FactoryRepository(db);
        var cropRepo = new Repository<CropType>(db);
        var supplyRepo = new Repository<SupplyRequest>(db);
        var matchRepo = new Repository<FarmMatch>(db);
        var contractRepo = new Repository<Contract>(db);
        var messageRepo = new Repository<Message>(db);
        var notificationRepo = new Repository<Notification>(db);
        var uow = new UnitOfWork(db);
        var pdf = new FakePdfService();

        var factoryDocRepo = new Repository<FactoryDocument>(db);
        var service = new FactoryService(
            factoryRepo,
            cropRepo,
            factoryDocRepo,
            supplyRepo,
            matchRepo,
            contractRepo,
            messageRepo,
            notificationRepo,
            null!,
            pdf,
            new FakeFulfillmentService(),
            new FakePaymentMilestoneService(),
            new FakeDisputeService(),
            new NoopWalletService(),
            new NileChain.Tests.TestDoubles.NoopEscrowPayments(),
            new NoopIntegrityService(),
            uow);

        var request = new CreateSupplyRequestRequest
        {
            Crop = "Wheat",
            Quantity = 10,
            Price = 1000,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(14),
            IdempotencyKey = "key-abc-123"
        };

        var first = await service.CreateRequestAsync(userId, request, "key-abc-123");
        var second = await service.CreateRequestAsync(userId, request, "key-abc-123");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.RequestId, second.Value.RequestId);
        Assert.Equal(1, await db.SupplyRequests.CountAsync());
    }

    private sealed class FakePdfService : IContractPdfService
    {
        public byte[] GeneratePdf(NileChain.Application.Dtos.Contracts.ContractPdfModel model) =>
            Array.Empty<byte>();

        public byte[] GeneratePdf(
            string title,
            string contractText,
            string farmName,
            string factoryName,
            bool factorySigned = false,
            bool farmSigned = false,
            DateTime? factorySignedAt = null,
            DateTime? farmSignedAt = null) => Array.Empty<byte>();
    }

    private sealed class FakeFulfillmentService : NileChain.Application.Interfaces.IFulfillmentService
    {
        public Task EnsureCreatedForSignedContractAsync(
            Guid contractId,
            Guid actorUserId,
            DateTime? plannedShipDate = null,
            NileChain.Domain.Enums.DeliveryPoint? deliveryPoint = null,
            NileChain.Domain.Enums.DealParty? freightPayer = null,
            NileChain.Domain.Enums.DealParty? transitRisk = null) =>
            Task.CompletedTask;

        public Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason) =>
            Task.CompletedTask;

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.FulfillmentDto>>
            GetByContractAsync(Guid userId, Guid contractId, bool asFarm) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.FulfillmentDto>>
            MarkShippedAsync(
                Guid farmUserId,
                Guid contractId,
                NileChain.Application.Dtos.Fulfillment.ShipFulfillmentRequest? request = null) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.FulfillmentDto>>
            MarkReceivedAsync(
                Guid factoryUserId,
                Guid contractId,
                NileChain.Application.Dtos.Fulfillment.ReceiveFulfillmentRequest? request = null) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.FulfillmentDto>>
            MarkRejectedAtGateAsync(
                Guid factoryUserId,
                Guid contractId,
                NileChain.Application.Dtos.Fulfillment.RejectAtGateRequest request) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.FulfillmentDto>>
            MarkQualityCheckedAsync(
                Guid factoryUserId,
                Guid contractId,
                NileChain.Application.Dtos.Fulfillment.QualityCheckRequest? request = null) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.FulfillmentDto>>
            MarkFulfilledAsync(Guid factoryUserId, Guid contractId) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Fulfillment.StuckFulfillmentListDto>>
            GetStuckDeliveriesAsync(int page, int pageSize) =>
            throw new NotImplementedException();
    }

    private sealed class FakePaymentMilestoneService : NileChain.Application.Interfaces.IPaymentMilestoneService
    {
        public Task EnsureCreatedForSignedContractAsync(
            Guid contractId,
            Guid actorUserId,
            NileChain.Domain.Entities.SupplyRequest? supplyRequest) =>
            Task.CompletedTask;

        public Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason) =>
            Task.CompletedTask;

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Payment.PaymentMilestoneScheduleDto>>
            GetByContractAsync(Guid userId, Guid contractId, bool asFarm) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Payment.PaymentMilestoneScheduleDto>>
            MarkPaidAsync(
                Guid factoryUserId,
                Guid contractId,
                Guid transactionId,
                Microsoft.AspNetCore.Http.IFormFile? receipt = null) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Payment.PaymentMilestoneScheduleDto>>
            ConfirmReceivedAsync(Guid farmUserId, Guid contractId, Guid transactionId) =>
            throw new NotImplementedException();
    }

    private sealed class FakeDisputeService : NileChain.Application.Interfaces.IDisputeService
    {
        public Task<bool> HasActiveDisputeAsync(Guid contractId) => Task.FromResult(false);

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeDto>> OpenAsync(
            Guid userId,
            Guid contractId,
            bool asFarm,
            string type,
            string description,
            IReadOnlyList<Microsoft.AspNetCore.Http.IFormFile>? evidenceFiles) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<IReadOnlyList<NileChain.Application.Dtos.Dispute.DisputeDto>>>
            ListForContractAsync(Guid userId, Guid contractId, bool asFarm) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeDto>>
            GetAsync(Guid userId, Guid disputeId, bool asFarm) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeListDto>>
            ListAdminAsync(string? status, string? type, int page, int pageSize) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeDto>>
            GetAdminAsync(Guid disputeId) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeDto>>
            MoveToUnderReviewAsync(Guid adminUserId, Guid disputeId, string? adminNote) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeDto>>
            ResolveAsync(Guid adminUserId, Guid disputeId, string adminNote, string outcomeFavor) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeDto>>
            RejectAsync(Guid adminUserId, Guid disputeId, string adminNote) =>
            throw new NotImplementedException();

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Dispute.DisputeListDto>>
            ListMineAsync(Guid userId, bool asFarm, string? status, int page, int pageSize) =>
            throw new NotImplementedException();
    }

    private sealed class NoopIntegrityService : NileChain.Application.Interfaces.IContractIntegrityService
    {
        public Task AnchorIfFullySignedAsync(NileChain.Domain.Entities.Contract contract) =>
            Task.CompletedTask;

        public Task SupersedeActiveAsync(Guid contractId) => Task.CompletedTask;

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Integrity.ContractIntegrityDto>>
            GetActiveForContractAsync(Guid contractId) =>
            Task.FromResult(
                NileChain.Application.Common.Result<NileChain.Application.Dtos.Integrity.ContractIntegrityDto>
                    .Failure(NileChain.Application.Errors.IntegrityErrors.NotAnchored));

        public Task<NileChain.Application.Dtos.Integrity.ContractIntegrityVerifyDto> VerifyByHashAsync(
            string contentHash) =>
            Task.FromResult(new NileChain.Application.Dtos.Integrity.ContractIntegrityVerifyDto
            {
                Outcome = "NotFound",
                ContentHash = contentHash
            });
    }

    private sealed class NoopWalletService : NileChain.Application.Interfaces.IWalletService
    {
        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> GetMineAsync(
            Guid userId, bool asFarm) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.NotFound));

        public Task<NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>> PaySubscriptionMonthAsync(
            Guid userId, bool asFarm) =>
            Task.FromResult(NileChain.Application.Common.Result<NileChain.Application.Dtos.Wallet.WalletDto>.Failure(
                NileChain.Application.Errors.WalletErrors.SubscriptionFarmNotApplicable));

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

        public Task<NileChain.Application.Common.Result<Guid>> HoldForEscrowAsync(
            Guid factoryId, decimal amountEgp, Guid escrowTransactionId, string description) =>
            Task.FromResult(NileChain.Application.Common.Result<Guid>.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public Task<NileChain.Application.Common.Result> EnsureFactoryAvailableAsync(
            Guid factoryId, decimal amountEgp) =>
            Task.FromResult(NileChain.Application.Common.Result.Success());

        public Task<NileChain.Application.Common.Result<Guid>> HoldDealFundsAsync(
            Guid factoryId, Guid contractId, decimal amountEgp, string description) =>
            Task.FromResult(NileChain.Application.Common.Result<Guid>.Failure(
                NileChain.Application.Errors.WalletErrors.InsufficientBalance));

        public Task<NileChain.Application.Common.Result> ReleaseEscrowToFarmAsync(
            Guid factoryId, Guid farmId, decimal totalHeldEgp, decimal farmNetEgp, Guid escrowTransactionId) =>
            Task.FromResult(NileChain.Application.Common.Result.Failure(
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

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NileChain.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "NileChain.Infrastructure")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend root.");
    }
}
