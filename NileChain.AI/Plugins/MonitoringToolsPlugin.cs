using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NileChain.AI.Orchestration;
using NileChain.AI.Weather;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Plugins;

/// <summary>
/// KernelFunctions for the farm-side proactive monitoring agent.
/// </summary>
public sealed class MonitoringToolsPlugin
{
    public const string ContractMarkerPrefix = "#contract:";
    public const string ContractMarkerSuffix = "#";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly NileChainDbContext _db;
    private readonly ILogger _logger;
    private readonly MonitoringRunState _state;
    private readonly IWeatherRiskClient _weather;

    public MonitoringRunState State => _state;

    public MonitoringToolsPlugin(
        NileChainDbContext db,
        ILogger logger,
        MonitoringRunState state,
        IWeatherRiskClient weather)
    {
        _db = db;
        _logger = logger;
        _state = state;
        _weather = weather;
    }

    [KernelFunction("GetActiveContracts")]
    [Description(
        "Returns all contracts currently in an active/signed state " +
        "(Signed only — excludes Draft, PendingSignature, and Cancelled). " +
        "Each item includes contractId, farmId, factoryId, farmUserId, factoryUserId, " +
        "cropType, lockedPricePerTon, deliveryDate, and governorate. " +
        "Call this FIRST before checking weather or market risk.")]
    public async Task<string> GetActiveContracts()
    {
        const string args = "";
        try
        {
            // Assumption: "active" = ContractStatus.Signed (domain has no Active/Completed status).
            var rows = await _db.Contracts
                .AsNoTracking()
                .Where(c => c.Status == ContractStatus.Signed)
                .Select(c => new
                {
                    c.ContractId,
                    FarmId = c.FarmMatch.FarmId,
                    FactoryId = c.FarmMatch.SupplyRequest.FactoryId,
                    FarmUserId = c.FarmMatch.Farm.UserId,
                    FactoryUserId = c.FarmMatch.SupplyRequest.Factory.UserId,
                    CropType = c.FarmMatch.SupplyRequest.CropType.Name,
                    LockedPricePerTon = c.FarmMatch.SupplyRequest.PricePerTon,
                    DeliveryDate = c.FarmMatch.SupplyRequest.DeliveryDate,
                    Governorate = c.FarmMatch.SupplyRequest.Factory.Governorate
                                  ?? c.FarmMatch.Farm.Governorate
                                  ?? "Unknown",
                    FarmLatitude = c.FarmMatch.Farm.Latitude,
                    FarmLongitude = c.FarmMatch.Farm.Longitude,
                    FactoryLatitude = c.FarmMatch.SupplyRequest.Factory.Latitude,
                    FactoryLongitude = c.FarmMatch.SupplyRequest.Factory.Longitude
                })
                .ToListAsync();

            var summary = $"activeContracts={rows.Count}";
            _state.RecordTrail("GetActiveContracts", args, summary);
            _logger.LogInformation("Tool GetActiveContracts | {Summary}", summary);

            return JsonSerializer.Serialize(new
            {
                count = rows.Count,
                contracts = rows.Select(r => new
                {
                    contractId = r.ContractId,
                    farmId = r.FarmId,
                    factoryId = r.FactoryId,
                    farmUserId = r.FarmUserId,
                    factoryUserId = r.FactoryUserId,
                    cropType = r.CropType,
                    lockedPricePerTon = r.LockedPricePerTon,
                    deliveryDate = r.DeliveryDate,
                    governorate = r.Governorate,
                    latitude = r.FarmLatitude ?? r.FactoryLatitude,
                    longitude = r.FarmLongitude ?? r.FactoryLongitude
                })
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("GetActiveContracts", args, $"error={ex.Message}");
            _logger.LogWarning(ex, "GetActiveContracts failed");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction("CheckWeatherRisk")]
    [Description(
        "Assess weather-related delivery risk for a governorate and delivery date. " +
        "Use when deliveryDate is within the next 14 days. " +
        "Returns riskLevel (Low|Medium|High) and a short reason. " +
        "Only High risk should trigger a WeatherRisk alert.")]
    public async Task<string> CheckWeatherRisk(
        [Description("Egyptian governorate name for the delivery / farm area")] string governorate,
        [Description("Contract delivery date (ISO-8601)")] DateTime deliveryDate)
    {
        var args = $"governorate={governorate}; deliveryDate={deliveryDate:yyyy-MM-dd}";
        try
        {
            var assessed = await _weather.AssessAsync(governorate, deliveryDate, null, null);
            var summary = $"riskLevel={assessed.RiskLevel}; live={assessed.FromLiveFeed}; reason={assessed.Reason}";
            _state.RecordTrail("CheckWeatherRisk", args, summary);
            _logger.LogInformation("Tool CheckWeatherRisk | {Args} | {Summary}", args, summary);

            return JsonSerializer.Serialize(new
            {
                governorate,
                deliveryDate = deliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                riskLevel = assessed.RiskLevel,
                reason = assessed.Reason,
                simulated = !assessed.FromLiveFeed,
                latitude = assessed.Latitude,
                longitude = assessed.Longitude
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("CheckWeatherRisk", args, $"error={ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction("CheckMarketPriceShift")]
    [Description(
        "Compare a contract's locked pricePerTon against the most recent MarketPrice " +
        "for the same crop type (any governorate; most recent RecordedAt wins). " +
        "Returns percentageShift (positive = market above locked). " +
        "Alert when abs(percentageShift) >= 15.")]
    public async Task<string> CheckMarketPriceShift(
        [Description("Crop type name, e.g. Wheat, Potato")] string cropType,
        [Description("Locked contract price per ton in EGP from GetActiveContracts")]
        double lockedPricePerTon)
    {
        var locked = (decimal)lockedPricePerTon;
        var args = $"cropType={cropType}; lockedPricePerTon={locked}";
        try
        {
            if (locked <= 0)
            {
                var empty = "lockedPricePerTon must be > 0";
                _state.RecordTrail("CheckMarketPriceShift", args, empty, blocked: true, blockReason: empty);
                return JsonSerializer.Serialize(new { error = empty }, JsonOptions);
            }

            // Assumption: "most recent" = latest MarketPrice.RecordedAt for this crop name (case-insensitive),
            // across all governorates.
            var latest = await _db.MarketPrices
                .AsNoTracking()
                .Where(p => p.CropType.Name.ToLower() == cropType.ToLower())
                .OrderByDescending(p => p.RecordedAt)
                .Select(p => new
                {
                    p.PricePerTon,
                    p.RecordedAt,
                    p.Governorate,
                    CropName = p.CropType.Name
                })
                .FirstOrDefaultAsync();

            if (latest is null)
            {
                var summary = "no MarketPrice rows for crop";
                _state.RecordTrail("CheckMarketPriceShift", args, summary);
                return JsonSerializer.Serialize(new
                {
                    cropType,
                    lockedPricePerTon = locked,
                    percentageShift = (decimal?)null,
                    reason = summary
                }, JsonOptions);
            }

            var shift = Math.Round(
                (latest.PricePerTon - locked) / locked * 100m,
                2);

            var resultSummary =
                $"market={latest.PricePerTon:0.##}; locked={locked:0.##}; " +
                $"shift={shift:0.##}%; recordedAt={latest.RecordedAt:yyyy-MM-dd}";
            _state.RecordTrail("CheckMarketPriceShift", args, resultSummary);
            _logger.LogInformation("Tool CheckMarketPriceShift | {Args} | {Summary}", args, resultSummary);

            return JsonSerializer.Serialize(new
            {
                cropType = latest.CropName,
                lockedPricePerTon = locked,
                latestMarketPricePerTon = latest.PricePerTon,
                marketGovernorate = latest.Governorate,
                marketRecordedAt = latest.RecordedAt,
                percentageShift = shift,
                alertWorthy = Math.Abs(shift) >= MonitoringRunState.PriceShiftAlertThresholdPercent
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("CheckMarketPriceShift", args, $"error={ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction("SendRiskAlert")]
    [Description(
        "Create a Notification for a farm or factory user about contract risk. " +
        "alertType should be WeatherRisk or PriceShift. " +
        "Call HasRecentAlert first to avoid spam. " +
        "Send to BOTH farmUserId and factoryUserId when a risk is confirmed " +
        "(two separate calls). Cap: max 20 SendRiskAlert calls per run.")]
    public async Task<string> SendRiskAlert(
        [Description("Contract GUID")] Guid contractId,
        [Description("Recipient ApplicationUser Id (farm or factory user)")] Guid recipientUserId,
        [Description("Alert type: WeatherRisk or PriceShift")] string alertType,
        [Description("Human-readable alert message")] string message)
    {
        var args =
            $"contractId={contractId}; recipientUserId={recipientUserId}; " +
            $"alertType={alertType}; message={Truncate(message, 80)}";

        if (!_state.TryConsumeSendAlertSlot(out var blockReason))
        {
            _state.RecordTrail("SendRiskAlert", args, blockReason!, blocked: true, blockReason: blockReason);
            return JsonSerializer.Serialize(new { sent = false, blocked = true, reason = blockReason }, JsonOptions);
        }

        try
        {
            var normalizedType = NormalizeAlertType(alertType);
            var title = normalizedType switch
            {
                "WeatherRisk" => "Weather risk on active contract",
                "PriceShift" => "Market price shift on active contract",
                _ => "Contract risk alert"
            };

            // Embed contract id so HasRecentAlert can dedupe without a schema change.
            var body =
                $"{ContractMarkerPrefix}{contractId}{ContractMarkerSuffix} {message}".Trim();

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = recipientUserId,
                Title = title,
                Message = body,
                Type = normalizedType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _db.Notifications.AddAsync(notification);
            await _db.SaveChangesAsync();

            var summary = $"sent=true; notificationId={notification.NotificationId}; type={normalizedType}";
            _state.RecordTrail("SendRiskAlert", args, summary);
            _logger.LogInformation("Tool SendRiskAlert | {Args} | {Summary}", args, summary);

            return JsonSerializer.Serialize(new
            {
                sent = true,
                notificationId = notification.NotificationId,
                alertType = normalizedType,
                recipientUserId,
                contractId
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("SendRiskAlert", args, $"error={ex.Message}");
            return JsonSerializer.Serialize(new { sent = false, error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction("HasRecentAlert")]
    [Description(
        "Returns whether an alert of the given type was already sent for this contract " +
        "within the last withinHours hours (default 24). " +
        "Always call this before SendRiskAlert.")]
    public async Task<string> HasRecentAlert(
        [Description("Contract GUID")] Guid contractId,
        [Description("Alert type: WeatherRisk or PriceShift")] string alertType,
        [Description("Look-back window in hours (use 24)")] int withinHours)
    {
        var hours = withinHours <= 0
            ? MonitoringRunState.DefaultAlertDedupeHours
            : withinHours;
        var args = $"contractId={contractId}; alertType={alertType}; withinHours={hours}";

        try
        {
            var normalizedType = NormalizeAlertType(alertType);
            var marker = $"{ContractMarkerPrefix}{contractId}{ContractMarkerSuffix}";
            var since = DateTime.UtcNow.AddHours(-hours);

            var exists = await _db.Notifications
                .AsNoTracking()
                .AnyAsync(n =>
                    n.Type == normalizedType
                    && n.CreatedAt >= since
                    && n.Message != null
                    && n.Message.Contains(marker));

            var summary = $"hasRecent={exists}";
            _state.RecordTrail("HasRecentAlert", args, summary);
            _logger.LogInformation("Tool HasRecentAlert | {Args} | {Summary}", args, summary);

            return JsonSerializer.Serialize(new
            {
                contractId,
                alertType = normalizedType,
                withinHours = hours,
                hasRecentAlert = exists
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("HasRecentAlert", args, $"error={ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static string NormalizeAlertType(string alertType)
    {
        if (string.Equals(alertType, "WeatherRisk", StringComparison.OrdinalIgnoreCase))
            return "WeatherRisk";
        if (string.Equals(alertType, "PriceShift", StringComparison.OrdinalIgnoreCase))
            return "PriceShift";
        return string.IsNullOrWhiteSpace(alertType) ? "WeatherRisk" : alertType.Trim();
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }
}
