using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace SkySync.Gateway.Resilience;

/// <summary>
/// Resilience Policies - Circuit Breaker, Retry, Timeout
/// Senior Level: Resilience Pattern - Service failure protection
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Timeout Policy - İsteklerin maksimum süresi
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(IConfiguration? configuration = null, ILogger? logger = null)
    {
        var timeoutSeconds = configuration?.GetValue<int>("ResilienceSettings:TimeoutSeconds") ?? 30;
        
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout: TimeSpan.FromSeconds(timeoutSeconds),
            timeoutStrategy: TimeoutStrategy.Pessimistic, // Timeout olursa exception fırlat
            onTimeoutAsync: (context, timespan, task, exception) =>
            {
                logger?.LogWarning("Request timeout after {Timeout} seconds", timespan.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Retry Policy - Başarısız istekleri tekrar dener
    /// Exponential Backoff: 2^retryAttempt saniye bekle
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(IConfiguration? configuration = null, ILogger? logger = null)
    {
        var retryCount = configuration?.GetValue<int>("ResilienceSettings:RetryCount") ?? 3;
        
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TimeoutRejectedException>() // Timeout exception'ları da retry et
            .WaitAndRetryAsync(
                retryCount: retryCount,
                sleepDurationProvider: retryAttempt => 
                {
                    // Exponential backoff: 2s, 4s, 8s
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    logger?.LogInformation("Retry attempt {RetryAttempt} after {Delay} seconds", retryAttempt, delay.TotalSeconds);
                    return delay;
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var status = outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.GetType().Name ?? "Unknown";
                    logger?.LogWarning(
                        "Retry {RetryCount} after {Timespan} seconds. Status: {Status}",
                        retryCount, timespan.TotalSeconds, status);
                });
    }

    /// <summary>
    /// Circuit Breaker Policy - Hata sonrası circuit açılır
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(IConfiguration? configuration = null, ILogger? logger = null)
    {
        var failureThreshold = configuration?.GetValue<int>("ResilienceSettings:CircuitBreakerFailureThreshold") ?? 5;
        var durationSeconds = configuration?.GetValue<int>("ResilienceSettings:CircuitBreakerDurationSeconds") ?? 30;
        
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(durationSeconds),
                onBreak: (result, duration) =>
                {
                    var status = result.Result?.StatusCode.ToString() ?? result.Exception?.GetType().Name ?? "Unknown";
                    logger?.LogError(
                        "Circuit breaker opened. Duration: {Duration} seconds. Status: {Status}",
                        duration.TotalSeconds, status);
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit breaker reset. Service is healthy again.");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit breaker half-open. Testing service health...");
                });
    }

    /// <summary>
    /// Combined Policy - Timeout + Retry + Circuit Breaker
    /// Senior Level: Resilience Pattern - Tüm koruma mekanizmaları bir arada
    /// Policy sırası: Timeout -> Retry -> Circuit Breaker
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(IConfiguration? configuration = null, ILogger? logger = null)
    {
        var timeoutPolicy = CreateTimeoutPolicy(configuration, logger);
        var retryPolicy = CreateRetryPolicy(configuration, logger);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy(configuration, logger);

        // Policy sırası: Timeout -> Retry -> Circuit Breaker
        // Önce timeout kontrol edilir, sonra retry, en son circuit breaker
        return Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }
}
