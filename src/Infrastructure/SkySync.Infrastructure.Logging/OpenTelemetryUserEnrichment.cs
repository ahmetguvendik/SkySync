using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// OpenTelemetry span'lere UserId ve UserEmail attribute'ları ekler.
/// AspNetCore instrumentation Enrich callback ile kullanılır.
/// Gateway: JWT'den, Backend: X-User-Id / X-User-Email header'larından okur.
/// </summary>
public static class OpenTelemetryUserEnrichment
{
    private const string UserIdHeaderName = "X-User-Id";
    private const string UserEmailHeaderName = "X-User-Email";

    public static void EnrichActivityWithUser(Activity activity, HttpContext context)
    {
        if (activity == null || context == null) return;

        string? userId = null;
        string? userEmail = null;

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;
            userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value;
        }

        if (string.IsNullOrEmpty(userId))
            userId = context.Request.Headers[UserIdHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(userEmail))
            userEmail = context.Request.Headers[UserEmailHeaderName].FirstOrDefault();

        if (!string.IsNullOrEmpty(userId))
            activity.SetTag("user.id", userId);
        if (!string.IsNullOrEmpty(userEmail))
            activity.SetTag("user.email", userEmail);
    }
}
