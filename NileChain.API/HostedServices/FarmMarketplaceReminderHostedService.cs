using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.API.HostedServices;

/// <summary>
/// Daily reminders for overdue payment milestones and expiring farm certifications.
/// Status-tracking / trust signals only — not a payment gateway.
/// </summary>
public sealed class FarmMarketplaceReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FarmMarketplaceReminderHostedService> _logger;

    public FarmMarketplaceReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<FarmMarketplaceReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NileChainDbContext>();
                var result = await RunReminderPassAsync(db, DateTime.UtcNow, stoppingToken);
                _logger.LogInformation(
                    "Farm marketplace reminders overdue={Overdue} certExpiring={Certs}",
                    result.OverdueNotifications,
                    result.CertNotifications);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Farm marketplace reminder hosted run failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static async Task<(int OverdueNotifications, int CertNotifications)> RunReminderPassAsync(
        NileChainDbContext db,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var overdue = await SendOverdueRemindersAsync(db, utcNow, ct);
        var certs = await SendCertExpiryRemindersAsync(db, utcNow, ct);
        if (overdue + certs > 0)
            await db.SaveChangesAsync(ct);
        return (overdue, certs);
    }

    private static async Task<int> SendOverdueRemindersAsync(
        NileChainDbContext db,
        DateTime utcNow,
        CancellationToken ct)
    {
        var today = utcNow.Date;
        var dayStart = today;
        var dayEnd = today.AddDays(1);

        var overdue = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.Farm)
            .Where(t =>
                t.DueDate != null
                && t.DueDate.Value.Date < today
                && (t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.MarkedPaid)
                && t.Contract.Status == ContractStatus.Signed)
            .ToListAsync(ct);

        var created = 0;
        foreach (var tx in overdue)
        {
            var farmUserId = tx.Contract.FarmMatch?.Farm?.UserId;
            if (farmUserId is null || farmUserId == Guid.Empty)
                continue;

            var refKey = tx.TransactionId.ToString("N");
            var already = await db.Notifications.AnyAsync(n =>
                n.UserId == farmUserId
                && n.Type == "PaymentOverdue"
                && n.Message.Contains(refKey)
                && n.CreatedAt >= dayStart
                && n.CreatedAt < dayEnd, ct);
            if (already)
                continue;

            await db.Notifications.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = farmUserId.Value,
                Title = "Payment overdue",
                Message =
                    $"Milestone '{tx.Label}' is past due ({tx.DueDate:yyyy-MM-dd}). Ref {refKey}. Status tracking only — follow up with the factory offline.",
                Type = "PaymentOverdue",
                IsRead = false,
                CreatedAt = utcNow
            }, ct);
            created++;
        }

        return created;
    }

    private static async Task<int> SendCertExpiryRemindersAsync(
        NileChainDbContext db,
        DateTime utcNow,
        CancellationToken ct)
    {
        var windowEnd = utcNow.AddDays(30);
        var dayStart = utcNow.Date;
        var dayEnd = dayStart.AddDays(1);

        var certs = await db.FarmCertifications
            .AsNoTracking()
            .Include(c => c.Farm)
            .Include(c => c.Certification)
            .Where(c =>
                c.ExpiresAt != null
                && c.ExpiresAt > utcNow
                && c.ExpiresAt <= windowEnd)
            .ToListAsync(ct);

        var created = 0;
        foreach (var cert in certs)
        {
            var farmUserId = cert.Farm?.UserId;
            if (farmUserId is null || farmUserId == Guid.Empty)
                continue;

            var refKey = $"{cert.FarmId:N}:{cert.CertificationId:N}";
            var already = await db.Notifications.AnyAsync(n =>
                n.UserId == farmUserId
                && n.Type == "CertExpiring"
                && n.Message.Contains(refKey)
                && n.CreatedAt >= dayStart
                && n.CreatedAt < dayEnd, ct);
            if (already)
                continue;

            var name = cert.Certification?.Name ?? "Certification";
            await db.Notifications.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = farmUserId.Value,
                Title = "Certification expiring soon",
                Message =
                    $"{name} expires on {cert.ExpiresAt:yyyy-MM-dd}. Renew it to keep match trust high. Ref {refKey}.",
                Type = "CertExpiring",
                IsRead = false,
                CreatedAt = utcNow
            }, ct);
            created++;
        }

        return created;
    }
}
