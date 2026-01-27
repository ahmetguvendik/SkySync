using System.Diagnostics;

namespace SkySync.Gateway.Middleware;

/// <summary>
/// API Gateway Request Logging Middleware
/// Tüm gelen istekleri loglar (Senior Level: Observability)
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;
        var clientIp = context.Connection.RemoteIpAddress?.ToString();

        // Correlation ID ve Transaction ID'yi context'ten al
        // (CorrelationIdMiddleware bunları önceden set etmiş olmalı)
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "N/A";
        var transactionId = context.Items["TransactionId"]?.ToString() ?? "N/A";

        _logger.LogInformation(
            "Incoming request: {Method} {Path} from {ClientIp} | CorrelationId: {CorrelationId} | TransactionId: {TransactionId}",
            requestMethod, requestPath, clientIp, correlationId, transactionId);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Request completed: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | CorrelationId: {CorrelationId} | TransactionId: {TransactionId}",
                requestMethod, requestPath, context.Response.StatusCode, stopwatch.ElapsedMilliseconds, correlationId, transactionId);
        }
    }
}
