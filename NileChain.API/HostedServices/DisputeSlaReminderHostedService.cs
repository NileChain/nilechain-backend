using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Common;
using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;

namespace NileChain.API.HostedServices;

/// <summary>
/// Reminds admins of open disputes past the 48h SLA. Does not auto-resolve or move money.
/// </summary>
public sealed class DisputeSlaReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DisputeSlaReminderHostedService> _logger;

    public DisputeSlaReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DisputeSlaReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(70), stoppingToken);
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
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var count = await RunPassAsync(db, users, DateTime.UtcNow, stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Dispute SLA reminders sent {Count}", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Dispute SLA reminder hosted run failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static async Task<int> RunPassAsync(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var overdue = await db.Disputes
            .Where(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview)
            .ToListAsync(ct);

        overdue = overdue.Where(d => DisputeSla.IsOverdue(d, utcNow)).ToList();
        if (overdue.Count == 0)
            return 0;

        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var supers = await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        var recipients = admins.Concat(supers).Select(u => u.Id).Distinct().ToList();
        if (recipients.Count == 0)
            return 0;

        var created = 0;
        var dayStart = utcNow.Date;
        var dayEnd = dayStart.AddDays(1);

        foreach (var dispute in overdue)
        {
            foreach (var adminId in recipients)
            {
                var already = await db.Notifications.AnyAsync(n =>
                    n.UserId == adminId
                    && n.Type == "DisputeSlaOverdue"
                    && n.RelatedEntityId == dispute.DisputeId
                    && n.CreatedAt >= dayStart
                    && n.CreatedAt < dayEnd, ct);
                if (already)
                    continue;

                await db.Notifications.AddAsync(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = adminId,
                    Title = "Dispute SLA overdue",
                    Message =
                        $"Dispute {dispute.DisputeId:N} is past the 48-hour review SLA. Resolve or reject — funds stay frozen until then.",
                    Type = "DisputeSlaOverdue",
                    RelatedEntityType = "Dispute",
                    RelatedEntityId = dispute.DisputeId,
                    IsRead = false,
                    CreatedAt = utcNow
                }, ct);
                created++;
            }
        }

        if (created > 0)
            await db.SaveChangesAsync(ct);

        return created;
    }
}
