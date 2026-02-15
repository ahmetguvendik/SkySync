using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// Correlation/Transaction ID üretimi için ortak yardımcı.
/// </summary>
public static class CorrelationContextResolver
{
    public static string GetOrCreateCorrelationId(HttpContext context, ILogger? logger = null)
    {
        if (TryReadHeader(context, CorrelationHeaderNames.CorrelationId, out var value))
        {
            logger?.LogDebug("Correlation ID received from client: {CorrelationId}", value);
            return value;
        }

        if (TryReadHeader(context, CorrelationHeaderNames.RequestId, out value))
        {
            logger?.LogDebug("Correlation ID received from client (X-Request-ID): {CorrelationId}", value);
            return value;
        }

        var activityId = Activity.Current?.Id;
        if (!string.IsNullOrWhiteSpace(activityId))
        {
            logger?.LogDebug("Correlation ID generated from Activity.Current: {CorrelationId}", activityId);
            return activityId!;
        }

        var generated = Guid.NewGuid().ToString();
        logger?.LogDebug("New Correlation ID generated: {CorrelationId}", generated);
        return generated;
    }

    public static string GetOrCreateTransactionId(HttpContext context, bool preferExisting = true)
    {
        if (preferExisting && TryReadHeader(context, CorrelationHeaderNames.TransactionId, out var value))
        {
            return value;
        }

        return Guid.NewGuid().ToString();
    }

    private static bool TryReadHeader(HttpContext context, string headerName, out string value)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            value = headerValue.ToString();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
