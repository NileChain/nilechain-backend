namespace NileChain.AI.Resilience;

/// <summary>
/// Minimal process-wide circuit breaker for LLM invocations (no Polly dependency).
/// Opens after consecutive failures; allows one half-open probe after <see cref="OpenDuration"/>.
/// </summary>
public sealed class LlmCircuitBreaker
{
    public const string ClientSafeUnavailable = "AI service unavailable";

    public static LlmCircuitBreaker Shared { get; } = new();

    private readonly object _gate = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private int _consecutiveFailures;
    private DateTime? _openedUtc;
    private bool _halfOpenProbeInFlight;

    public LlmCircuitBreaker(int failureThreshold = 5, TimeSpan? openDuration = null)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>True while the circuit rejects calls (cooldown) or while open awaiting recovery.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_gate)
                return _openedUtc is not null;
        }
    }

    /// <summary>Returns false when the circuit is open and no probe is allowed.</summary>
    public bool TryEnter(out string? rejectReason)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;

            if (_openedUtc is null)
            {
                rejectReason = null;
                return true;
            }

            if (now - _openedUtc.Value < _openDuration)
            {
                rejectReason = ClientSafeUnavailable;
                return false;
            }

            // Cooldown elapsed — allow a single half-open probe.
            if (_halfOpenProbeInFlight)
            {
                rejectReason = ClientSafeUnavailable;
                return false;
            }

            _halfOpenProbeInFlight = true;
            rejectReason = null;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openedUtc = null;
            _halfOpenProbeInFlight = false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _halfOpenProbeInFlight = false;
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failureThreshold)
                _openedUtc = DateTime.UtcNow;
        }
    }

    /// <summary>Test helper — reset state between unit tests.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openedUtc = null;
            _halfOpenProbeInFlight = false;
        }
    }
}
