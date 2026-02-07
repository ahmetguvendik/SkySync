using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// Request header'lardan CorrelationId ve TransactionId okur ve Serilog LogContext'e ekler.
/// Böylece tüm log satırları otomatik olarak bu property'lere sahip olur (Seq'de filtreleme için).
/// Gateway'den gelen isteklerde X-Correlation-ID ve X-Transaction-ID header'ları ile taşınır.
/// </summary>
public sealed class CorrelationIdLogContextMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string TransactionIdHeaderName = "X-Transaction-ID";
    private const string RequestIdHeaderName = "X-Request-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdLogContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        var transactionId = GetOrCreateTransactionId(context);

        context.Items["CorrelationId"] = correlationId;
        context.Items["TransactionId"] = transactionId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TransactionId", transactionId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.ToString()!;

        if (context.Request.Headers.TryGetValue(RequestIdHeaderName, out val) && !string.IsNullOrWhiteSpace(val))
            return val.ToString()!;

        return Activity.Current?.Id ?? Guid.NewGuid().ToString();
    }

    private static string GetOrCreateTransactionId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TransactionIdHeaderName, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.ToString()!;

        return Guid.NewGuid().ToString();
    }
}

/// <summary>
/// CorrelationIdLogContextMiddleware extension methods.
/// </summary>
public static class CorrelationIdLogContextMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationIdLogContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdLogContextMiddleware>();
    }
}
