namespace SkySync.Gateway.Middleware;

/// <summary>
/// Request/Response Transformation Middleware
/// Senior Level: Request/Response manipulation, Header injection
/// 
/// Performance Optimized: MemoryStream kullanmıyor, sadece header ekliyor
/// Büyük response'lar için memory-safe (OnStarting kullanıyor)
/// </summary>
public class RequestTransformationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTransformationMiddleware> _logger;

    public RequestTransformationMiddleware(RequestDelegate next, ILogger<RequestTransformationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestStartTime = DateTime.UtcNow;

        // Request Transformation: Gateway bilgisi ekle
        if (!context.Request.Headers.ContainsKey("X-Gateway-Version"))
        {
            context.Request.Headers["X-Gateway-Version"] = "1.0.0";
        }

        if (!context.Request.Headers.ContainsKey("X-Gateway-Timestamp"))
        {
            context.Request.Headers["X-Gateway-Timestamp"] = requestStartTime.ToString("O");
        }

        // Original IP'yi koru (X-Forwarded-For)
        var originalIp = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(originalIp) && !context.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            context.Request.Headers["X-Forwarded-For"] = originalIp;
        }

        // Response Transformation: OnStarting kullan (Memory-safe, büyük response'lar için güvenli)
        // Bu yaklaşım response body'yi memory'ye yüklemez, sadece header ekler
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Gateway-Processed"))
            {
                context.Response.Headers["X-Gateway-Processed"] = "true";
            }

            if (!context.Response.Headers.ContainsKey("X-Response-Time"))
            {
                var responseTime = DateTime.UtcNow;
                context.Response.Headers["X-Response-Time"] = responseTime.ToString("O");
            }

            if (!context.Response.Headers.ContainsKey("X-Request-Duration"))
            {
                var duration = (DateTime.UtcNow - requestStartTime).TotalMilliseconds;
                context.Response.Headers["X-Request-Duration"] = $"{duration:F2}ms";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
