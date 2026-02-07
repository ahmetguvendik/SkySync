namespace SkySync.Gateway.Configuration;

/// <summary>
/// Polly resilience pattern configuration for Gateway downstream HTTP calls.
/// </summary>
public sealed class PollyResilienceOptions
{
    public const string SectionName = "PollyResilience";

    /// <summary>
    /// Total request timeout in seconds (outermost policy).
    /// Default: 30
    /// </summary>
    public int TotalRequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts on transient failures.
    /// Default: 3
    /// </summary>
    public int RetryMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay between retries in seconds (exponential backoff base).
    /// Default: 1
    /// </summary>
    public double RetryDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Per-attempt timeout in seconds.
    /// Default: 10
    /// </summary>
    public int AttemptTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Circuit breaker: failure ratio (0.0 - 1.0) to open the circuit.
    /// Default: 0.1 (10%)
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.1;

    /// <summary>
    /// Circuit breaker: sampling duration in seconds.
    /// Default: 30
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Circuit breaker: minimum throughput before evaluation.
    /// Default: 10
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Circuit breaker: duration in seconds the circuit stays open.
    /// Default: 5
    /// </summary>
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 5;
}
