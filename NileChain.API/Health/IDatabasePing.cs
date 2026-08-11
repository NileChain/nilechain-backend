using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NileChain.API.Health;

/// <summary>Lightweight DB reachability probe used by <c>GET /health</c>.</summary>
public interface IDatabasePing
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
