using System.Collections.Concurrent;
using System.Net;

namespace NileChain.API.Middleware;

/// <summary>
/// Lightweight in-memory rate limit for auth endpoints (login / register / refresh).
/// </summary>
public sealed class AuthRateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, Window> Windows = new();
    private readonly RequestDelegate _next;
    private readonly int _limit;
    private readonly TimeSpan _window;

    public AuthRateLimitMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _limit = configuration.GetValue("AuthRateLimit:MaxRequests", 30);
        _window = TimeSpan.FromMinutes(configuration.GetValue("AuthRateLimit:WindowMinutes", 1));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsAuthPath(path))
        {
            await _next(context);
            return;
        }

        var key = $"{context.Connection.RemoteIpAddress}|{path}|{context.Request.Method}";
        var now = DateTime.UtcNow;
        var window = Windows.AddOrUpdate(
            key,
            _ => new Window(now, 1),
            (_, existing) =>
            {
                if (now - existing.StartedAt > _window)
                    return new Window(now, 1);
                return existing with { Count = existing.Count + 1 };
            });

        if (window.Count > _limit)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "Auth.RateLimited",
                message = "Too many authentication attempts. Please try again shortly."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsAuthPath(string path) =>
        path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/api/auth/register", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase);

    private sealed record Window(DateTime StartedAt, int Count);
}
