using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// UserId ve UserEmail'i LogContext'e ekler. Uçuş ekleme ve rezervasyon loglarında audit için kullanılır.
/// Gateway: JWT'den okur, X-User-Id / X-User-Email header'ları ile backend'e iletir.
/// Backend: Header'lardan okur (Gateway'den forward edilmiş).
/// Pipeline'da UseAuthentication'dan SONRA çalışmalıdır.
/// </summary>
public sealed class UserLogContextMiddleware
{
    private const string UserIdHeaderName = "X-User-Id";
    private const string UserEmailHeaderName = "X-User-Email";

    private readonly RequestDelegate _next;

    public UserLogContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var (userId, userEmail) = GetUserInfo(context);

        if (!string.IsNullOrEmpty(userId))
        {
            context.Items["UserId"] = userId;
            context.Request.Headers[UserIdHeaderName] = userId;
        }

        if (!string.IsNullOrEmpty(userEmail))
        {
            context.Items["UserEmail"] = userEmail;
            context.Request.Headers[UserEmailHeaderName] = userEmail;
        }

        if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(userEmail))
        {
            using (LogContext.PushProperty("UserId", userId ?? ""))
            using (LogContext.PushProperty("UserEmail", userEmail ?? ""))
            {
                await _next(context);
            }
        }
        else
        {
            await _next(context);
        }
    }

    private static (string? UserId, string? UserEmail) GetUserInfo(HttpContext context)
    {
        // 1. Önce HttpContext.User'dan al (Gateway - JWT doğrulandı)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;
            var userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value;
            if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(userEmail))
                return (userId, userEmail);
        }

        // 2. Header'lardan al (Backend - Gateway'den forward edilmiş)
        var headerUserId = context.Request.Headers[UserIdHeaderName].FirstOrDefault();
        var headerUserEmail = context.Request.Headers[UserEmailHeaderName].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerUserId) || !string.IsNullOrEmpty(headerUserEmail))
            return (headerUserId, headerUserEmail);

        return (null, null);
    }
}

/// <summary>
/// UserLogContextMiddleware extension methods.
/// </summary>
public static class UserLogContextMiddlewareExtensions
{
    /// <summary>
    /// UserId ve UserEmail'i LogContext'e ekler. UseAuthentication'dan sonra çağrılmalı.
    /// </summary>
    public static IApplicationBuilder UseUserLogContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserLogContextMiddleware>();
    }
}
