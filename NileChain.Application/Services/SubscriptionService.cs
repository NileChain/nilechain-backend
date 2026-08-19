using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Billing;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    public const string HonestyNote =
        "Monthly quota funded from the NileChain wallet — not a bank standing order and not live card billing.";

    private readonly ISubscriptionRepository _subscriptions;
    private readonly IWalletService _wallets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SubscriptionOptions _options;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IWalletService wallets,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> users,
        IOptions<SubscriptionOptions> options)
    {
        _subscriptions = subscriptions;
        _wallets = wallets;
        _unitOfWork = unitOfWork;
        _users = users;
        _options = options.Value;
    }

    public async Task<Result<BillingMeDto>> GetMineAsync(
        Guid userId,
        bool asFarm,
        CancellationToken cancellationToken = default)
    {
        var role = await ResolveMarketplaceRoleAsync(userId, asFarm, cancellationToken);
        if (role.IsFailure)
            return Result<BillingMeDto>.Failure(role.Error!);

        var snapshot = await ResolveSnapshotAsync(userId, role.Value, DateTime.UtcNow, cancellationToken);
        return Result<BillingMeDto>.Success(await MapAsync(userId, snapshot, cancellationToken));
    }

    public async Task<Result<BillingMeDto>> SubscribeAsync(
        Guid userId,
        bool asFarm,
        CancellationToken cancellationToken = default)
    {
        var role = await ResolveMarketplaceRoleAsync(userId, asFarm, cancellationToken);
        if (role.IsFailure)
            return Result<BillingMeDto>.Failure(role.Error!);

        var paid = await _wallets.PaySubscriptionMonthAsync(userId, asFarm);
        if (paid.IsFailure)
            return Result<BillingMeDto>.Failure(paid.Error!);

        var now = DateTime.UtcNow;
        var paidThrough = paid.Value!.SubscriptionPaidThroughUtc ?? now.AddDays(30);
        await UpsertPlanAsync(
            userId,
            SubscriptionPlanCodes.ProForRole(asFarm),
            SubscriptionSource.Wallet,
            now,
            paidThrough,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync();

        var snapshot = await ResolveSnapshotAsync(userId, role.Value, DateTime.UtcNow, cancellationToken);
        return Result<BillingMeDto>.Success(await MapAsync(userId, snapshot, cancellationToken));
    }

    public async Task<Result<BillingMeDto>> AdminGrantAsync(
        Guid userId,
        string planCode,
        DateTime? periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (!SubscriptionPlanCodes.TryNormalize(planCode, out var normalized))
            return Result<BillingMeDto>.Failure(SubscriptionErrors.InvalidPlan);

        var asFarm = SubscriptionPlanCodes.IsFarm(normalized);
        var role = await ResolveMarketplaceRoleAsync(userId, asFarm, cancellationToken);
        if (role.IsFailure)
            return Result<BillingMeDto>.Failure(role.Error!);

        if (SubscriptionPlanCodes.IsFactory(normalized) && asFarm)
            return Result<BillingMeDto>.Failure(SubscriptionErrors.InvalidPlan);
        if (SubscriptionPlanCodes.IsFarm(normalized) && !asFarm)
            return Result<BillingMeDto>.Failure(SubscriptionErrors.InvalidPlan);

        var now = DateTime.UtcNow;
        if (SubscriptionPlanCodes.IsFree(normalized))
        {
            var latest = await _subscriptions.GetLatestAsync(userId, cancellationToken);
            if (latest is not null && latest.IsLiveAt(now))
            {
                latest.Status = SubscriptionStatus.Cancelled;
                latest.PeriodEnd = now;
                latest.UpdatedAt = now;
                _subscriptions.Update(latest);
                await _unitOfWork.SaveChangesAsync();
            }
        }
        else
        {
            var end = periodEndUtc is DateTime specified && specified > now
                ? DateTime.SpecifyKind(specified, DateTimeKind.Utc)
                : now.AddDays(30);
            await UpsertPlanAsync(
                userId,
                normalized,
                SubscriptionSource.AdminGrant,
                now,
                end,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync();
        }

        var snapshot = await ResolveSnapshotAsync(userId, role.Value, DateTime.UtcNow, cancellationToken);
        return Result<BillingMeDto>.Success(await MapAsync(userId, snapshot, cancellationToken));
    }

    public async Task<Result> EnsureCanConsumeAsync(
        Guid userId,
        SubscriptionMetric metric,
        CancellationToken cancellationToken = default)
    {
        var asFarm = metric == SubscriptionMetric.FarmAccepts;
        var role = await ResolveMarketplaceRoleAsync(userId, asFarm, cancellationToken);
        if (role.IsFailure)
            return role;

        var now = DateTime.UtcNow;
        var snapshot = await ResolveSnapshotAsync(userId, role.Value, now, cancellationToken);
        var cap = snapshot.Entitlements.CapFor(metric);
        if (cap is null)
            return Result.Success();

        var used = await GetUsedAsync(userId, snapshot.PeriodStart, metric, cancellationToken);
        if (used >= cap.Value)
            return Result.Failure(SubscriptionErrors.QuotaExceeded);

        return Result.Success();
    }

    public async Task ConsumeAsync(
        Guid userId,
        SubscriptionMetric metric,
        CancellationToken cancellationToken = default)
    {
        var asFarm = metric == SubscriptionMetric.FarmAccepts;
        var roleResult = await ResolveMarketplaceRoleAsync(userId, asFarm, cancellationToken);
        if (roleResult.IsFailure)
            return;

        var now = DateTime.UtcNow;
        var snapshot = await ResolveSnapshotAsync(userId, roleResult.Value, now, cancellationToken);
        var usage = await _subscriptions.GetUsageAsync(userId, snapshot.PeriodStart, metric, cancellationToken);
        if (usage is null)
        {
            await _subscriptions.AddUsageAsync(new SubscriptionUsage
            {
                UsageId = Guid.NewGuid(),
                UserId = userId,
                PeriodStart = snapshot.PeriodStart,
                Metric = metric,
                Count = 1,
                UpdatedAt = now
            }, cancellationToken);
        }
        else
        {
            usage.Count += 1;
            usage.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<Result> EnsureFeatureAsync(
        Guid userId,
        SubscriptionFeatureFlag flag,
        CancellationToken cancellationToken = default)
    {
        var asFarm = false;
        var role = await ResolveMarketplaceRoleAsync(userId, asFarm, cancellationToken);
        if (role.IsFailure)
            return role;

        var snapshot = await ResolveSnapshotAsync(userId, role.Value, DateTime.UtcNow, cancellationToken);
        if (snapshot.Entitlements.Flag(flag))
            return Result.Success();

        return Result.Failure(SubscriptionErrors.PlanRequired);
    }

    private async Task<Result<bool>> ResolveMarketplaceRoleAsync(
        Guid userId,
        bool asFarm,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<bool>.Failure(SubscriptionErrors.UserNotFound);

        var roles = await _users.GetRolesAsync(user);
        var isFarm = roles.Contains(NileChain.Domain.Constants.AppRoles.Farm, StringComparer.OrdinalIgnoreCase);
        var isFactory = roles.Contains(NileChain.Domain.Constants.AppRoles.Factory, StringComparer.OrdinalIgnoreCase);
        if (!isFarm && !isFactory)
            return Result<bool>.Failure(SubscriptionErrors.NotApplicable);

        if (asFarm && !isFarm)
            return Result<bool>.Failure(SubscriptionErrors.NotApplicable);
        if (!asFarm && !isFactory)
            return Result<bool>.Failure(SubscriptionErrors.NotApplicable);

        return Result<bool>.Success(asFarm);
    }

    private async Task UpsertPlanAsync(
        Guid userId,
        string planCode,
        SubscriptionSource source,
        DateTime now,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var latest = await _subscriptions.GetLatestAsync(userId, cancellationToken);
        if (latest is not null && latest.IsLiveAt(now) &&
            string.Equals(latest.PlanCode, planCode, StringComparison.OrdinalIgnoreCase))
        {
            latest.PeriodEnd = periodEnd > latest.PeriodEnd ? periodEnd : latest.PeriodEnd;
            latest.Source = source;
            latest.Status = SubscriptionStatus.Active;
            latest.UpdatedAt = now;
            _subscriptions.Update(latest);
            return;
        }

        if (latest is not null && latest.IsLiveAt(now))
        {
            latest.Status = SubscriptionStatus.Cancelled;
            latest.PeriodEnd = now;
            latest.UpdatedAt = now;
            _subscriptions.Update(latest);
        }

        await _subscriptions.AddAsync(new Subscription
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = userId,
            PlanCode = planCode,
            Status = SubscriptionStatus.Active,
            PeriodStart = now,
            PeriodEnd = periodEnd,
            Source = source,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }

    private async Task<ResolvedPlan> ResolveSnapshotAsync(
        Guid userId,
        bool asFarm,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var live = await _subscriptions.GetLiveAsync(userId, utcNow, cancellationToken);
        var config = _options.ToPlanConfig();
        if (live is not null && SubscriptionPlanCodes.IsPro(live.PlanCode))
        {
            return new ResolvedPlan(
                live.PlanCode,
                live.Status.ToString(),
                live.Source.ToString(),
                live.PeriodStart,
                live.PeriodEnd,
                SubscriptionEntitlementsResolver.For(live.PlanCode, config));
        }

        var periodStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var free = SubscriptionEntitlementsResolver.FreeForRole(asFarm, config);
        return new ResolvedPlan(
            free.PlanCode,
            SubscriptionStatus.Active.ToString(),
            "Implied",
            periodStart,
            periodEnd,
            free);
    }

    private async Task<int> GetUsedAsync(
        Guid userId,
        DateTime periodStart,
        SubscriptionMetric metric,
        CancellationToken cancellationToken)
    {
        var usage = await _subscriptions.GetUsageAsync(userId, periodStart, metric, cancellationToken);
        return usage?.Count ?? 0;
    }

    private async Task<BillingMeDto> MapAsync(
        Guid userId,
        ResolvedPlan snapshot,
        CancellationToken cancellationToken)
    {
        async Task<BillingMeterDto> MeterAsync(SubscriptionMetric metric, int? cap)
        {
            var used = await GetUsedAsync(userId, snapshot.PeriodStart, metric, cancellationToken);
            return new BillingMeterDto
            {
                Metric = metric.ToString(),
                Used = used,
                Cap = cap,
                Remaining = cap is int c ? Math.Max(0, c - used) : null
            };
        }

        var e = snapshot.Entitlements;
        return new BillingMeDto
        {
            Role = SubscriptionPlanCodes.IsFarm(e.PlanCode) ? "Farm" : "Factory",
            PlanCode = e.PlanCode,
            Status = snapshot.Status,
            Source = snapshot.Source,
            PeriodStart = snapshot.PeriodStart,
            PeriodEnd = snapshot.PeriodEnd,
            ProPriceEgp = SubscriptionPlanCodes.IsFarm(e.PlanCode)
                ? _options.FarmProEgp
                : _options.FactoryProEgp,
            IsPro = SubscriptionPlanCodes.IsPro(e.PlanCode),
            Copilot = e.Copilot,
            ShowMore = e.ShowMore,
            ExpandGeo = e.ExpandGeo,
            FactoryRfqs = await MeterAsync(SubscriptionMetric.FactoryRfqs, e.FactoryRfqs),
            AgentRuns = await MeterAsync(SubscriptionMetric.AgentRuns, e.AgentRuns),
            FarmAccepts = await MeterAsync(SubscriptionMetric.FarmAccepts, e.FarmAccepts),
            HonestyNote = HonestyNote
        };
    }

    private sealed record ResolvedPlan(
        string PlanCode,
        string Status,
        string Source,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        SubscriptionEntitlements Entitlements);
}
