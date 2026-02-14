using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SkySync.Infrastructure.Logging;

namespace SkySync.Infrastructure.Logging.Tests;

public class UserLogContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PrefersAuthenticatedUserClaimsAndForwardsHeaders()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "claims-user"),
                    new Claim(ClaimTypes.Email, "claims@skysync.dev")
                },
                authenticationType: "TestAuth"))
        };

        RequestDelegate next = httpContext =>
        {
            httpContext.Items["NextCalled"] = true;
            return Task.CompletedTask;
        };

        var middleware = new UserLogContextMiddleware(next);

        await middleware.InvokeAsync(context);

        context.Items["UserId"].Should().Be("claims-user");
        context.Items["UserEmail"].Should().Be("claims@skysync.dev");
        context.Request.Headers["X-User-Id"].ToString().Should().Be("claims-user");
        context.Request.Headers["X-User-Email"].ToString().Should().Be("claims@skysync.dev");
        context.Items["NextCalled"].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_FallsBackToHeadersWhenNoAuthenticatedUser()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "header-user";
        context.Request.Headers["X-User-Email"] = "header@skysync.dev";

        var middleware = new UserLogContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items["UserId"].Should().Be("header-user");
        context.Items["UserEmail"].Should().Be("header@skysync.dev");
    }

    [Fact]
    public async Task InvokeAsync_WhenNoUserInfo_DoesNotTouchHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new UserLogContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("UserId").Should().BeFalse();
        context.Items.ContainsKey("UserEmail").Should().BeFalse();
        context.Request.Headers.ContainsKey("X-User-Id").Should().BeFalse();
        context.Request.Headers.ContainsKey("X-User-Email").Should().BeFalse();
    }
}
