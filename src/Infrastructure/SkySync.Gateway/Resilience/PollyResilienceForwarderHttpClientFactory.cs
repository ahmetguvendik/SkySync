using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using SkySync.Gateway.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace SkySync.Gateway.Resilience;

/// <summary>
/// YARP ForwarderHttpClientFactory that wraps downstream HTTP calls with Polly resilience:
/// Retry (exponential backoff), Circuit Breaker, and Timeout patterns.
/// </summary>
public sealed class PollyResilienceForwarderHttpClientFactory(
    IOptions<PollyResilienceOptions> options,
    CircuitBreakerStateStore circuitBreakerStateStore,
    ILogger<PollyResilienceForwarderHttpClientFactory> logger)
    : ForwarderHttpClientFactory(logger)
{
    private readonly PollyResilienceOptions _options = options.Value;
    private readonly CircuitBreakerStateStore _circuitBreakerStateStore = circuitBreakerStateStore;
    private ResiliencePipeline<HttpResponseMessage>? _pipeline;

    private ResiliencePipeline<HttpResponseMessage> Pipeline => _pipeline ??= BuildPipeline();

    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        return new ResiliencePipelineHandler(handler, Pipeline);
    }

    private ResiliencePipeline<HttpResponseMessage> BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        // 1. Timeout - En dışta; tüm request için toplam timeout
        var totalTimeout = TimeSpan.FromSeconds(_options.TotalRequestTimeoutSeconds);
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = totalTimeout,
            Name = "TotalRequestTimeout",
            OnTimeout = args =>
            {
                logger.LogWarning(
                    "Polly TotalRequestTimeout: Request timed out after {TimeoutSeconds}s. OperationKey: {OperationKey}",
                    args.Timeout.TotalSeconds, args.Context.OperationKey);
                return default;
            }
        });

        // 2. Retry - Geçici hatalarda exponential backoff ile tekrar dene
        var maxRetries = _options.RetryMaxAttempts;
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = maxRetries,
            Delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Name = "Retry",
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(ShouldRetry)
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnRetry = args =>
            {
                var reason = args.Outcome.Exception?.Message
                    ?? args.Outcome.Result?.StatusCode.ToString()
                    ?? "Unknown";
                logger.LogWarning(
                    "Polly Retry: Attempt {Attempt}/{MaxAttempts} after {DelayMs}ms. Reason: {Reason}. OperationKey: {OperationKey}",
                    args.AttemptNumber, maxRetries, args.RetryDelay.TotalMilliseconds, reason, args.Context.OperationKey);
                return default;
            }
        });

        // 3. Circuit Breaker - Belirli hata sayısından sonra devreye gir, downstream'i koru
        var stateStore = _circuitBreakerStateStore;
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = _options.CircuitBreakerFailureRatio,
            SamplingDuration = TimeSpan.FromSeconds(_options.CircuitBreakerSamplingDurationSeconds),
            MinimumThroughput = _options.CircuitBreakerMinimumThroughput,
            BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerBreakDurationSeconds),
            Name = "CircuitBreaker",
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnOpened = args =>
            {
                stateStore.CurrentState = Polly.CircuitBreaker.CircuitState.Open;
                stateStore.OpenedAt = DateTimeOffset.UtcNow;
                var reason = args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString() ?? "Failure threshold exceeded";
                logger.LogWarning(
                    "Polly CircuitBreaker opened - downstream requests blocked. Reason: {Reason}. OperationKey: {OperationKey}",
                    reason, args.Context.OperationKey);
                return default;
            },
            OnClosed = args =>
            {
                stateStore.CurrentState = Polly.CircuitBreaker.CircuitState.Closed;
                stateStore.OpenedAt = null;
                logger.LogInformation(
                    "Polly CircuitBreaker closed - downstream requests resumed. OperationKey: {OperationKey}",
                    args.Context.OperationKey);
                return default;
            },
            OnHalfOpened = args =>
            {
                stateStore.CurrentState = Polly.CircuitBreaker.CircuitState.HalfOpen;
                logger.LogInformation(
                    "Polly CircuitBreaker half-open - probing downstream. OperationKey: {OperationKey}",
                    args.Context.OperationKey);
                return default;
            }
        });

        // 4. Attempt Timeout - Her deneme için maksimum süre
        var attemptTimeout = TimeSpan.FromSeconds(_options.AttemptTimeoutSeconds);
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = attemptTimeout,
            Name = "AttemptTimeout",
            OnTimeout = args =>
            {
                logger.LogWarning(
                    "Polly AttemptTimeout: Request attempt timed out after {TimeoutSeconds}s. OperationKey: {OperationKey}",
                    args.Timeout.TotalSeconds, args.Context.OperationKey);
                return default;
            }
        });

        return builder.Build();
    }

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        return response.StatusCode
            is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// DelegatingHandler that executes the inner handler through a Polly ResiliencePipeline.
    /// </summary>
    private sealed class ResiliencePipelineHandler(
        HttpMessageHandler innerHandler,
        ResiliencePipeline<HttpResponseMessage> pipeline)
        : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!IsSafeMethod(request.Method))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var context = ResilienceContextPool.Shared.Get(cancellationToken);
            try
            {
                return await pipeline.ExecuteAsync(
                    async ctx => await SendCoreAsync(request, ctx.CancellationToken),
                    context);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }

        private async Task<HttpResponseMessage> SendCoreAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        private static bool IsSafeMethod(HttpMethod method) =>
            method == HttpMethod.Get ||
            method == HttpMethod.Head ||
            method == HttpMethod.Options;
    }
}
