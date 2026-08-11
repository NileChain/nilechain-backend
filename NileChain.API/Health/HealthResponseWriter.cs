using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NileChain.API.Health;

/// <summary>
/// Minimal health JSON — never includes connection strings, versions, or exception details.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static object ToBody(HealthReport report) =>
        report.Status == HealthStatus.Healthy
            ? new { status = "healthy" }
            : new { status = "unhealthy", reason = "database" };

    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(ToBody(report), JsonOptions));
    }
}
