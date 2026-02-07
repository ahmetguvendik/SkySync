using System.Reflection;
using MassTransit;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// MassTransit consume filter: CorrelationId/MessageId'yi LogContext'e ekler.
/// Consumer logları Seq'de CorrelationId ile filtrelenebilir.
/// </summary>
public sealed class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) { }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var correlationId = ExtractCorrelationId(context);
        var messageId = context.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var logEventType = typeof(T).Name.EndsWith("Event") ? "IntegrationEvent" : "Command";

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        using (Serilog.Context.LogContext.PushProperty("MessageId", messageId))
        using (Serilog.Context.LogContext.PushProperty("LogEventType", logEventType))
        {
            await next.Send(context);
        }
    }

    private static string ExtractCorrelationId(ConsumeContext<T> context)
    {
        var msg = context.Message;
        if (msg == null) return context.MessageId?.ToString() ?? Guid.NewGuid().ToString();

        var prop = msg.GetType().GetProperty("CorrelationId", BindingFlags.Public | BindingFlags.Instance);
        var value = prop?.GetValue(msg);
        if (value is Guid guid && guid != Guid.Empty)
            return guid.ToString();

        return context.MessageId?.ToString() ?? Guid.NewGuid().ToString();
    }
}
