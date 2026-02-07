using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkySync.Gateway.Resilience;

namespace SkySync.Gateway.Health;

/// <summary>
/// Health check that reports circuit breaker state to /health endpoint.
/// - Healthy: Circuit is Closed
/// - Degraded: Circuit is HalfOpen (probing for recovery)
/// - Unhealthy: Circuit is Open (downstream calls blocked)
/// </summary>
public sealed class CircuitBreakerHealthCheck(CircuitBreakerStateStore stateStore) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var state = stateStore.CurrentState;
        var data = new Dictionary<string, object>
        {
            ["circuit_state"] = state.ToString()
        };

        if (stateStore.OpenedAt.HasValue)
        {
            data["opened_at"] = stateStore.OpenedAt.Value.ToString("O");
        }

        return state switch
        {
            Polly.CircuitBreaker.CircuitState.Closed => Task.FromResult(
                HealthCheckResult.Healthy("Circuit breaker is closed - downstream requests flowing normally.", data)),

            Polly.CircuitBreaker.CircuitState.HalfOpen => Task.FromResult(
                HealthCheckResult.Degraded("Circuit breaker is half-open - probing downstream for recovery.", data: data)),

            Polly.CircuitBreaker.CircuitState.Open => Task.FromResult(
                HealthCheckResult.Unhealthy("Circuit breaker is open - downstream requests blocked.", data: data)),

            _ => Task.FromResult(
                HealthCheckResult.Healthy("Unknown circuit state.", data))
        };
    }
}
