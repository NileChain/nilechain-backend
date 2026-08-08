using Microsoft.Extensions.Options;
using NileChain.AI;
using NileChain.AI.Agents;

namespace NileChain.API.HostedServices;

/// <summary>
/// Periodically runs <see cref="ProactiveMonitorAgent"/> over active contracts.
/// Interval is demo-friendly (default 2 minutes); production would typically use ~24h.
/// </summary>
public sealed class ProactiveMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MonitoringOptions> _options;
    private readonly ILogger<ProactiveMonitorHostedService> _logger;

    public ProactiveMonitorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MonitoringOptions> options,
        ILogger<ProactiveMonitorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
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
                _logger.LogDebug("Proactive monitoring disabled (Monitoring:Enabled=false); sleeping");
            }
            else
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var agent = scope.ServiceProvider.GetRequiredService<ProactiveMonitorAgent>();
                    var result = await agent.RunAsync(stoppingToken);
                    _logger.LogInformation(
                        "Proactive monitor run finished mode={Mode} success={Success} alerts={Alerts} contracts={Contracts}",
                        result.OrchestratorMode,
                        result.Success,
                        result.AlertsSent,
                        result.ActiveContractsReviewed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Proactive monitor hosted run failed");
                }
            }

            var minutes = Math.Max(1, _options.CurrentValue.IntervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
