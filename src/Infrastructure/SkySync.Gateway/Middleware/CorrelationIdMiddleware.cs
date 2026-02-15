using System.Diagnostics;
using Serilog.Context;
using SkySync.Infrastructure.Logging;

namespace SkySync.Gateway.Middleware;

/// <summary>
/// Correlation ID and Transaction ID Middleware
/// 
/// CORRELATION ID:
/// - Tüm request yaşam döngüsü boyunca aynı kalır
/// - Gateway'den başlayıp tüm mikroservislere taşınır
/// - Distributed tracing için kullanılır
/// - Örnek: User bir rezervasyon yaptı → Flight, Payment, Notification servisleri aynı Correlation ID'yi görür
/// 
/// TRANSACTION ID:
/// - Her HTTP request için unique ID
/// - API Gateway seviyesinde request tracking
/// - Kısa ömürlü (sadece bu request)
/// - Örnek: User 3 kez retry yaptı → 3 farklı Transaction ID ama aynı Correlation ID
/// 
/// Senior Level: Observability, Distributed Tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. CORRELATION ID: Client'tan geliyorsa kullan, yoksa oluştur
        // Bu ID tüm sistem boyunca aynı kalacak
        var correlationId = CorrelationContextResolver.GetOrCreateCorrelationId(context, _logger);

        // 2. TRANSACTION ID: Her request için yeni bir ID
        // Bu ID sadece bu HTTP request için geçerli
        var transactionId = CorrelationContextResolver.GetOrCreateTransactionId(context, preferExisting: false);

        // 3. Activity (OpenTelemetry/Distributed Tracing için)
        // .NET'in built-in tracing mekanizması
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation.id", correlationId);
            activity.SetTag("transaction.id", transactionId);
        }

        // 4. HttpContext.Items'a ekle (downstream middleware'ler ve controllerlar için)
        context.Items[CorrelationContextKeys.CorrelationId] = correlationId;
        context.Items[CorrelationContextKeys.TransactionId] = transactionId;

        // 5. Response header'a ekle (client debugging + distributed tracing için)
        // traceparent/tracestate: Frontend ödeme isteğinde gönderirse rezervasyon+ödeme aynı trace'te görünür
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationHeaderNames.CorrelationId))
                context.Response.Headers[CorrelationHeaderNames.CorrelationId] = correlationId;
            if (!context.Response.Headers.ContainsKey(CorrelationHeaderNames.TransactionId))
                context.Response.Headers[CorrelationHeaderNames.TransactionId] = transactionId;
            // W3C Trace Context - frontend bunu okuyup ödeme isteğinde traceparent header olarak gönderir
            if (activity != null && !context.Response.Headers.ContainsKey(CorrelationHeaderNames.TraceParent) && !string.IsNullOrEmpty(activity.Id))
            {
                context.Response.Headers[CorrelationHeaderNames.TraceParent] = activity.Id;
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                    context.Response.Headers[CorrelationHeaderNames.TraceState] = activity.TraceStateString;
            }
            return Task.CompletedTask;
        });

        // 6. Request header'a ekle (downstream servislere gönder)
        // YARP bu header'ları otomatik olarak mikroservislere iletir
        context.Request.Headers[CorrelationHeaderNames.CorrelationId] = correlationId;
        context.Request.Headers[CorrelationHeaderNames.TransactionId] = transactionId;

        // 7. Log (Structured logging ile)
        _logger.LogInformation(
            "Request started. Method: {Method}, Path: {Path}, CorrelationId: {CorrelationId}, TransactionId: {TransactionId}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            transactionId);

        try
        {
            // 8. Serilog LogContext'e ekle - tüm downstream loglar otomatik CorrelationId/TransactionId alır
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("TransactionId", transactionId))
            {
                await _next(context);
            }
        }
        catch (Exception ex)
        {
            // 9. Exception durumunda ID'leri logla (debugging için kritik!)
            _logger.LogError(ex,
                "Request failed. Method: {Method}, Path: {Path}, CorrelationId: {CorrelationId}, TransactionId: {TransactionId}",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                transactionId);
            throw;
        }
    }

}

/// <summary>
/// Extension method for easy registration
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
