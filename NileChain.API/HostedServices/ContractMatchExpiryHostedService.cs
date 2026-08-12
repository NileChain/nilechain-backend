using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NileChain.API.Options;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.API.HostedServices;

/// <summary>
/// Periodically expires stale Proposed matches and Cancelled pending-signature contracts.
/// </summary>
public sealed class ContractMatchExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ContractMatchExpiryOptions> _options;
    private readonly ILogger<ContractMatchExpiryHostedService> _logger;

    public ContractMatchExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ContractMatchExpiryOptions> options,
        ILogger<ContractMatchExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                _logger.LogDebug("Contract/match expiry disabled; sleeping");
            }
            else
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NileChainDbContext>();
                    var result = await RunExpiryPassAsync(db, opts.ExpiryDays, DateTime.UtcNow, stoppingToken);
                    _logger.LogInformation(
                        "Contract/match expiry pass finished matchesExpired={Matches} contractsCancelled={Contracts}",
                        result.MatchesExpired,
                        result.ContractsCancelled);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Contract/match expiry hosted run failed");
                }
            }

            var hours = Math.Max(1, _options.CurrentValue.IntervalHours);
            try
            {
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Core expiry pass (also used by tests via public static selection helpers).</summary>
    public static async Task<(int MatchesExpired, int ContractsCancelled)> RunExpiryPassAsync(
        NileChainDbContext db,
        int expiryDays,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var days = Math.Max(1, expiryDays);
        var cutoff = utcNow.AddDays(-days);

        var proposed = await db.FarmMatches
            .Include(m => m.Farm)
            .Where(m =>
                (m.Status == FarmMatchStatus.Proposed || m.Status == FarmMatchStatus.Countered)
                && m.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        var toExpire = ContractMatchExpiry.SelectExpiredProposedMatches(
            proposed,
            m => m.Status,
            m => m.CreatedAt,
            utcNow,
            days);

        foreach (var match in toExpire)
        {
            match.Status = FarmMatchStatus.Expired;
            var farmUserId = match.Farm?.UserId ?? Guid.Empty;
            if (farmUserId != Guid.Empty)
            {
                db.Notifications.Add(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = farmUserId,
                    Title = "Match expired",
                    Message = "A proposed match expired after the review window closed.",
                    Type = "MatchExpired",
                    IsRead = false,
                    CreatedAt = utcNow
                });
            }
        }

        var pendingContracts = await db.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m!.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m!.SupplyRequest)
                    .ThenInclude(r => r!.Factory)
            .Where(c =>
                (c.Status == ContractStatus.Draft
                 || c.Status == ContractStatus.PendingSignature
                 || c.Status == ContractStatus.PendingFarmSignature
                 || c.Status == ContractStatus.PendingFactorySignature)
                && c.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        var toCancel = ContractMatchExpiry.SelectExpiredPendingContracts(
            pendingContracts,
            c => c.Status,
            c => c.CreatedAt,
            utcNow,
            days);

        foreach (var contract in toCancel)
        {
            contract.Status = ContractStatus.Cancelled;
            contract.ClearSignatures();

            var farmUserId = contract.FarmMatch?.Farm?.UserId ?? Guid.Empty;
            var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId ?? Guid.Empty;

            if (farmUserId != Guid.Empty)
            {
                db.Notifications.Add(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = farmUserId,
                    Title = "Contract expired",
                    Message = "An unsigned contract was cancelled after the signature window closed.",
                    Type = "ContractExpired",
                    IsRead = false,
                    CreatedAt = utcNow
                });
            }

            if (factoryUserId != Guid.Empty && factoryUserId != farmUserId)
            {
                db.Notifications.Add(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = factoryUserId,
                    Title = "Contract expired",
                    Message = "An unsigned contract was cancelled after the signature window closed.",
                    Type = "ContractExpired",
                    IsRead = false,
                    CreatedAt = utcNow
                });
            }
        }

        var topUpCutoff = utcNow.AddHours(-Math.Max(1, 24));
        var staleTopUps = await db.WalletTopUps
            .Where(t =>
                (t.Status == WalletTopUpStatus.Created || t.Status == WalletTopUpStatus.Pending)
                && t.CreatedAt <= topUpCutoff)
            .ToListAsync(cancellationToken);
        foreach (var topUp in staleTopUps)
        {
            topUp.Status = WalletTopUpStatus.Expired;
            topUp.UpdatedAt = utcNow;
        }

        if (toExpire.Count > 0 || toCancel.Count > 0 || staleTopUps.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return (toExpire.Count, toCancel.Count);
    }
}
