using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NileChain.API.Health;

namespace NileChain.Tests;

public class HealthEndpointTests
{
    private sealed class StubPing(bool ok, bool throwOnCall = false) : IDatabasePing
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            if (throwOnCall)
                throw new InvalidOperationException("simulated connectivity failure");
            return Task.FromResult(ok);
        }
    }

    private static DatabaseHealthCheck CreateCheck(IDatabasePing ping)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ping);
        var provider = services.BuildServiceProvider();
        return new DatabaseHealthCheck(provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenDbReachable_ReturnsHealthy()
    {
        var check = CreateCheck(new StubPing(ok: true));
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenDbUnreachable_ReturnsUnhealthy()
    {
        var check = CreateCheck(new StubPing(ok: false));
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenPingThrows_ReturnsUnhealthyWithSafeDescription()
    {
        var check = CreateCheck(new StubPing(ok: false, throwOnCall: true));
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("database", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void HealthResponseWriter_Healthy_BodyIsMinimal()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    description: null,
                    duration: TimeSpan.Zero,
                    exception: null,
                    data: null)
            },
            totalDuration: TimeSpan.Zero);

        var body = HealthResponseWriter.ToBody(report);
        var json = System.Text.Json.JsonSerializer.Serialize(body);

        Assert.Contains("\"status\":\"healthy\"", json);
        Assert.DoesNotContain("reason", json);
        Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HealthResponseWriter_Unhealthy_BodyHasSafeReasonOnly()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    description: "database",
                    duration: TimeSpan.Zero,
                    exception: new InvalidOperationException("Server=secret;Password=leak"),
                    data: null)
            },
            totalDuration: TimeSpan.Zero);

        var body = HealthResponseWriter.ToBody(report);
        var json = System.Text.Json.JsonSerializer.Serialize(body);

        Assert.Contains("\"status\":\"unhealthy\"", json);
        Assert.Contains("\"reason\":\"database\"", json);
        Assert.DoesNotContain("Password", json);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("InvalidOperationException", json);
    }
}
