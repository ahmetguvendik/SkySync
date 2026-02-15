namespace SkySync.Infrastructure.Logging;

/// <summary>
/// ID header isimlerini tek merkezde toplar.
/// </summary>
public static class CorrelationHeaderNames
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string TransactionId = "X-Transaction-ID";
    public const string RequestId = "X-Request-ID";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
}

/// <summary>
/// HttpContext.Items anahtarları için sabitler.
/// </summary>
public static class CorrelationContextKeys
{
    public const string CorrelationId = nameof(CorrelationId);
    public const string TransactionId = nameof(TransactionId);
}
