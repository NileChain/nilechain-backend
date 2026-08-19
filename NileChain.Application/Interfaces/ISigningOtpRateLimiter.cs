namespace NileChain.Application.Interfaces;

public interface ISigningOtpRateLimiter
{
    /// <summary>Returns false when the user already used 3 requests in the current 10-minute window.</summary>
    bool TryAcquire(Guid userId);
}
