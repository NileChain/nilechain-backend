using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Application.Services;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;

namespace NileChain.Tests;

public class AdvancedElectronicSignatureTests
{
    [Fact]
    public void OtpHasher_IsDeterministic_AndDoesNotMatchWrongCode()
    {
        var hash = SigningOtpHasher.Hash("123456");
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, SigningOtpHasher.Hash("123456"));
        Assert.True(SigningOtpHasher.Matches(hash, "123456"));
        Assert.False(SigningOtpHasher.Matches(hash, "000000"));
        Assert.NotEqual(hash, SigningOtpHasher.Hash("123457"));
    }

    [Fact]
    public void ContentHasher_IsDeterministic_AndChangesWithText()
    {
        var payload = new CanonicalContractPayload(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Nile Farm",
            "Delta Factory",
            "Wheat",
            10m,
            12000m,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            "عقد توريد");

        var ha = ContractContentHasher.Compute(payload);
        var hb = ContractContentHasher.Compute(payload with { GeneratedText = "عقد معدل" });

        Assert.Equal(64, ha.Length);
        Assert.Equal(ha, ContractContentHasher.Compute(payload));
        Assert.NotEqual(ha, hb);
    }

    [Fact]
    public void ContentHasher_HashMatchesUntilGeneratedTextChanges()
    {
        var contract = SignedContract("original body");
        var hash = ContractContentHasher.Compute(ContractContentHasher.FromContract(contract));
        Assert.Equal(hash, ContractContentHasher.Compute(ContractContentHasher.FromContract(contract)));

        contract.GeneratedText = "tampered";
        var after = ContractContentHasher.Compute(ContractContentHasher.FromContract(contract));
        Assert.NotEqual(hash, after);
    }

    [Fact]
    public void HmacToken_Verifies_AndFailsOnTamper()
    {
        var hashService = CreateHashService();
        var userId = Guid.NewGuid();
        var signedAt = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var hash = new string('a', 64);
        var token = hashService.ComputeSignatureToken(hash, userId, signedAt);

        Assert.Equal(64, token.Length);
        Assert.True(hashService.VerifySignatureToken(hash, userId, signedAt, token));
        Assert.False(hashService.VerifySignatureToken(hash, userId, signedAt.AddSeconds(1), token));
        Assert.False(hashService.VerifySignatureToken(hash, Guid.NewGuid(), signedAt, token));
    }

    [Fact]
    public void RateLimiter_AllowsThree_ThenBlocks()
    {
        var limiter = new SigningOtpRateLimiter();
        var userId = Guid.NewGuid();

        Assert.True(limiter.TryAcquire(userId));
        Assert.True(limiter.TryAcquire(userId));
        Assert.True(limiter.TryAcquire(userId));
        Assert.False(limiter.TryAcquire(userId));
        Assert.True(limiter.TryAcquire(Guid.NewGuid()));
    }

    [Fact]
    public void ResultHttpMapper_MapsRateLimitedTo429_AndInvalidOtpTo400()
    {
        Assert.Equal(
            System.Net.HttpStatusCode.TooManyRequests,
            ResultHttpMapper.MapStatus(SigningOtpErrors.RateLimited));
        Assert.Equal(
            System.Net.HttpStatusCode.BadRequest,
            ResultHttpMapper.MapStatus(SigningOtpErrors.Invalid));
    }

    [Fact]
    public async Task VerifyAndConsume_SucceedsOnce_ThenFails()
    {
        var userId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var otp = new SigningOtp
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            UserId = userId,
            OtpHash = SigningOtpHasher.Hash("654321"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        var repo = new FakeOtpRepository(otp);
        var service = CreateOtpService(repo);

        var ok = await service.VerifyAndConsumeAsync(contractId, userId, "654321");
        Assert.True(ok.IsSuccess);
        Assert.True(otp.IsUsed);

        var again = await service.VerifyAndConsumeAsync(contractId, userId, "654321");
        Assert.True(again.IsFailure);
        Assert.Equal(SigningOtpErrors.Invalid, again.Error);
    }

    [Fact]
    public async Task VerifyAndConsume_RejectsExpiredAndWrongCode()
    {
        var userId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var otp = new SigningOtp
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            UserId = userId,
            OtpHash = SigningOtpHasher.Hash("111222"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };
        var service = CreateOtpService(new FakeOtpRepository(otp));

        var expired = await service.VerifyAndConsumeAsync(contractId, userId, "111222");
        Assert.True(expired.IsFailure);

        otp.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
        var wrong = await service.VerifyAndConsumeAsync(contractId, userId, "000000");
        Assert.True(wrong.IsFailure);
        Assert.False(otp.IsUsed);
    }

    private static Contract SignedContract(string body)
    {
        var farm = new Farm { FarmId = Guid.NewGuid(), Name = "Integrity Farm" };
        var factory = new Factory { FactoryId = Guid.NewGuid(), Name = "Integrity Factory" };
        var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = "Rice" };
        var supply = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            Factory = factory,
            CropTypeId = crop.CropTypeId,
            CropType = crop,
            QuantityTons = 5,
            PricePerTon = 9000,
            DeliveryDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var match = new FarmMatch
        {
            MatchId = Guid.NewGuid(),
            Farm = farm,
            FarmId = farm.FarmId,
            SupplyRequest = supply,
            RequestId = supply.RequestId
        };
        return new Contract
        {
            ContractId = Guid.NewGuid(),
            MatchId = match.MatchId,
            FarmMatch = match,
            GeneratedText = body
        };
    }

    private static ContractHashService CreateHashService() =>
        new(
            Options.Create(new SigningOptions
            {
                HmacSecret = "THIS_IS_DEVELOPMENT_SECRET_KEY_CHANGE_IT_2026_123456789"
            }),
            new FakeSignatureRepository());

    private static SigningOtpService CreateOtpService(ISigningOtpRepository otps) =>
        new(
            otps,
            new FakeSignatureRepository(),
            CreateHashService(),
            new FakeUnitOfWork(),
            new FakeEmail(),
            new SigningOtpRateLimiter(),
            null!,
            NullLogger<SigningOtpService>.Instance);

    private sealed class FakeOtpRepository : ISigningOtpRepository
    {
        private readonly SigningOtp? _otp;
        public FakeOtpRepository(SigningOtp? otp) => _otp = otp;

        public Task<SigningOtp?> GetLatestUnusedAsync(Guid userId, Guid contractId) =>
            Task.FromResult(
                _otp is not null && !_otp.IsUsed && _otp.UserId == userId && _otp.ContractId == contractId
                    ? _otp
                    : null);

        public Task<List<SigningOtp>> GetUnusedForUserContractAsync(Guid userId, Guid contractId) =>
            Task.FromResult(
                _otp is not null && !_otp.IsUsed ? new List<SigningOtp> { _otp } : new List<SigningOtp>());

        public Task AddAsync(SigningOtp otp) => Task.CompletedTask;

        public Task<Contract?> GetContractForPartyAsync(Guid contractId, Guid userId) =>
            Task.FromResult<Contract?>(null);
    }

    private sealed class FakeSignatureRepository : IContractSignatureRepository
    {
        public Task AddSignatureAsync(ContractSignatureRecord record) => Task.CompletedTask;
        public Task AddAuditAsync(ContractAuditLog log) => Task.CompletedTask;
        public Task<ContractAuditLog?> GetLatestAuditAsync(Guid contractId) =>
            Task.FromResult<ContractAuditLog?>(null);
        public Task<List<ContractSignatureRecord>> GetSignaturesForContractAsync(Guid contractId) =>
            Task.FromResult(new List<ContractSignatureRecord>());
        public Task<List<ContractAuditLog>> GetAuditTrailAsync(Guid contractId) =>
            Task.FromResult(new List<ContractAuditLog>());
        public Task<Contract?> GetContractWithPartiesAsync(Guid contractId) =>
            Task.FromResult<Contract?>(null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeEmail : IEmailService
    {
        public Task SendAsync(EmailMessage message) => Task.CompletedTask;
    }
}
