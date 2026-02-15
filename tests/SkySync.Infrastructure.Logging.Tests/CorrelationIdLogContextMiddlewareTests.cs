using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SkySync.Infrastructure.Logging;

namespace SkySync.Infrastructure.Logging.Tests;

public class CorrelationIdLogContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_UsesHeadersWhenTheyExist()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationHeaderNames.CorrelationId] = "corr-123";
        context.Request.Headers[CorrelationHeaderNames.TransactionId] = "txn-456";
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdLogContextMiddleware(next);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[CorrelationContextKeys.CorrelationId].Should().Be("corr-123");
        context.Items[CorrelationContextKeys.TransactionId].Should().Be("txn-456");
    }

    [Fact]
    public async Task InvokeAsync_UsesActivityIdWhenHeadersAreMissing()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdLogContextMiddleware(next);

        using var activity = new Activity("corr-test");
        activity.Start();

        try
        {
            await middleware.InvokeAsync(context);
        }
        finally
        {
            activity.Stop();
        }

        nextCalled.Should().BeTrue();
        context.Items[CorrelationContextKeys.CorrelationId].Should().Be(activity.Id);
        context.Items[CorrelationContextKeys.TransactionId].Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(context.Items[CorrelationContextKeys.TransactionId]?.ToString(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UsesRequestIdHeaderWhenCorrelationHeaderMissing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationHeaderNames.RequestId] = "req-123";
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdLogContextMiddleware(next);
        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[CorrelationContextKeys.CorrelationId].Should().Be("req-123");
        context.Items[CorrelationContextKeys.TransactionId].Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationAndTransactionIdsWhenNoContextExists()
    {
        var context = new DefaultHttpContext();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdLogContextMiddleware(next);

        await middleware.InvokeAsync(context);

        var correlationId = context.Items[CorrelationContextKeys.CorrelationId]?.ToString();
        var transactionId = context.Items[CorrelationContextKeys.TransactionId]?.ToString();

        correlationId.Should().NotBeNullOrWhiteSpace();
        transactionId.Should().NotBeNullOrWhiteSpace();

        Guid.TryParse(correlationId, out _).Should().BeTrue();
        Guid.TryParse(transactionId, out _).Should().BeTrue();
    }
}
