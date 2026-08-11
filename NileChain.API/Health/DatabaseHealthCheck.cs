using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NileChain.API.Health;

/// <summary>Reports Healthy when the database accepts a connection; otherwise Unhealthy.</summary>
public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ping = scope.ServiceProvider.GetRequiredService<IDatabasePing>();
            var ok = await ping.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("database");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("database");
        }
    }
}
