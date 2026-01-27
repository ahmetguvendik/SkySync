using System.Diagnostics;

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

    // Standard header isimleri
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string TransactionIdHeaderName = "X-Transaction-ID";
    private const string RequestIdHeaderName = "X-Request-ID"; // Alternatif isim (bazı sistemler bunu kullanır)

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. CORRELATION ID: Client'tan geliyorsa kullan, yoksa oluştur
        // Bu ID tüm sistem boyunca aynı kalacak
        var correlationId = GetOrCreateCorrelationId(context);

        // 2. TRANSACTION ID: Her request için yeni bir ID
        // Bu ID sadece bu HTTP request için geçerli
        var transactionId = Guid.NewGuid().ToString();

        // 3. Activity (OpenTelemetry/Distributed Tracing için)
        // .NET'in built-in tracing mekanizması
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation.id", correlationId);
            activity.SetTag("transaction.id", transactionId);
        }

        // 4. HttpContext.Items'a ekle (downstream middleware'ler ve controllerlar için)
        context.Items["CorrelationId"] = correlationId;
        context.Items["TransactionId"] = transactionId;

        // 5. Response header'a ekle (client debugging için)
        // Client bu ID'leri görebilir ve support'a bildirebilir
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            }
            if (!context.Response.Headers.ContainsKey(TransactionIdHeaderName))
            {
                context.Response.Headers[TransactionIdHeaderName] = transactionId;
            }
            return Task.CompletedTask;
        });

        // 6. Request header'a ekle (downstream servislere gönder)
        // YARP bu header'ları otomatik olarak mikroservislere iletir
        context.Request.Headers[CorrelationIdHeaderName] = correlationId;
        context.Request.Headers[TransactionIdHeaderName] = transactionId;

        // 7. Log (Structured logging ile)
        _logger.LogInformation(
            "Request started. Method: {Method}, Path: {Path}, CorrelationId: {CorrelationId}, TransactionId: {TransactionId}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            transactionId);

        try
        {
            // 8. Next middleware'e geç
            await _next(context);
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

    /// <summary>
    /// Correlation ID'yi client'tan al veya yeni oluştur
    /// </summary>
    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // 1. Request header'dan al (client göndermiş)
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) 
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogDebug("Correlation ID received from client: {CorrelationId}", correlationId.ToString());
            return correlationId.ToString();
        }

        // 2. Alternatif header isimlerini kontrol et (X-Request-ID)
        if (context.Request.Headers.TryGetValue(RequestIdHeaderName, out var requestId) 
            && !string.IsNullOrWhiteSpace(requestId))
        {
            _logger.LogDebug("Correlation ID received from client (X-Request-ID): {CorrelationId}", requestId.ToString());
            return requestId.ToString();
        }

        // 3. Activity.Current.Id'yi kullan (ASP.NET Core otomatik oluşturur)
        if (Activity.Current?.Id != null)
        {
            _logger.LogDebug("Correlation ID generated from Activity.Current: {CorrelationId}", Activity.Current.Id);
            return Activity.Current.Id;
        }

        // 4. Yeni GUID oluştur (fallback)
        var newCorrelationId = Guid.NewGuid().ToString();
        _logger.LogDebug("New Correlation ID generated: {CorrelationId}", newCorrelationId);
        return newCorrelationId;
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
