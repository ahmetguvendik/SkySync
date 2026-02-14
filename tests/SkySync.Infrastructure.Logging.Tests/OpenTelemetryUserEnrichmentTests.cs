using System.Diagnostics;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SkySync.Infrastructure.Logging;

namespace SkySync.Infrastructure.Logging.Tests;

public class OpenTelemetryUserEnrichmentTests
{
    [Fact]
    public void EnrichActivityWithUser_UsesAuthenticatedUserClaimsWhenAvailable()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "user-123"),
                    new Claim(ClaimTypes.Email, "pilot@skysync.dev")
                },
                authenticationType: "TestAuth",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role))
        };

        using var activity = new Activity("test");
        activity.Start();
        try
        {
            OpenTelemetryUserEnrichment.EnrichActivityWithUser(activity, context);

            activity.GetTagItem("user.id").Should().Be("user-123");
            activity.GetTagItem("user.email").Should().Be("pilot@skysync.dev");
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void EnrichActivityWithUser_FallsBackToHeadersWhenUserNotAuthenticated()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "header-user";
        context.Request.Headers["X-User-Email"] = "header@skysync.dev";

        using var activity = new Activity("header-test");
        activity.Start();
        try
        {
            OpenTelemetryUserEnrichment.EnrichActivityWithUser(activity, context);

            activity.GetTagItem("user.id").Should().Be("header-user");
            activity.GetTagItem("user.email").Should().Be("header@skysync.dev");
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void EnrichActivityWithUser_DoesNothingWhenNoUserInformationExists()
    {
        var context = new DefaultHttpContext();
        using var activity = new Activity("empty-test");
        activity.Start();
        try
        {
            OpenTelemetryUserEnrichment.EnrichActivityWithUser(activity, context);

            activity.GetTagItem("user.id").Should().BeNull();
            activity.GetTagItem("user.email").Should().BeNull();
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void EnrichActivityWithUser_PrefersClaimsOverHeaders()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim("sub", "claim-priority"),
                    new Claim("email", "claim@skysync.dev")
                },
                authenticationType: "TestAuth"))
        };
        context.Request.Headers["X-User-Id"] = "header-user";
        context.Request.Headers["X-User-Email"] = "header@skysync.dev";

        using var activity = new Activity("priority-test");
        activity.Start();
        try
        {
            OpenTelemetryUserEnrichment.EnrichActivityWithUser(activity, context);

            activity.GetTagItem("user.id").Should().Be("claim-priority");
            activity.GetTagItem("user.email").Should().Be("claim@skysync.dev");
        }
        finally
        {
            activity.Stop();
        }
    }
}
