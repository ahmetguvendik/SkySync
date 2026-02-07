using Polly.CircuitBreaker;

namespace SkySync.Gateway.Resilience;

/// <summary>
/// Thread-safe store for the Circuit Breaker state, used by health checks.
/// </summary>
public sealed class CircuitBreakerStateStore
{
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();

    public CircuitState CurrentState
    {
        get { lock (_lock) return _state; }
        internal set { lock (_lock) _state = value; }
    }

    /// <summary>
    /// Timestamp when the circuit last transitioned to Open state.
    /// </summary>
    public DateTimeOffset? OpenedAt { get; internal set; }
}
