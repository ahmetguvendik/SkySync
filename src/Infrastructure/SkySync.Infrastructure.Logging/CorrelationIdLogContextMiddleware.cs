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
    private readonly RequestDelegate _next;

    public CorrelationIdLogContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = CorrelationContextResolver.GetOrCreateCorrelationId(context);
        var transactionId = CorrelationContextResolver.GetOrCreateTransactionId(context);

        context.Items[CorrelationContextKeys.CorrelationId] = correlationId;
        context.Items[CorrelationContextKeys.TransactionId] = transactionId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TransactionId", transactionId))
        {
            await _next(context);
        }
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
