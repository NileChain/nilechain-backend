using System.Collections.Concurrent;
using NileChain.Application.Interfaces;

namespace NileChain.Application.Services;

/// <summary>In-memory 3 requests per 10 minutes per user for signing OTP.</summary>
public sealed class SigningOtpRateLimiter : ISigningOtpRateLimiter
{
    private const int Limit = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<Guid, WindowState> _windows = new();

    public bool TryAcquire(Guid userId)
    {
        var now = DateTime.UtcNow;
        var state = _windows.AddOrUpdate(
            userId,
            _ => new WindowState(now, 1),
            (_, existing) =>
            {
                if (now - existing.StartedAt > Window)
                    return new WindowState(now, 1);
                return existing with { Count = existing.Count + 1 };
            });

        return state.Count <= Limit;
    }

    private sealed record WindowState(DateTime StartedAt, int Count);
}
