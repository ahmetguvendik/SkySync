using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// Merkezi Serilog konfigürasyonu.
/// Tüm SkySync servisleri (Gateway, Flight, Reservation vb.) tek tip loglama kullanır.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Host builder için Serilog konfigürasyonu.
    /// builder.Host.UseSerilog((ctx, lc) => SerilogConfiguration.Configure(ctx, lc, "Flight"));
    /// </summary>
    /// <param name="context">Host builder context</param>
    /// <param name="loggerConfig">Serilog LoggerConfiguration</param>
    /// <param name="serviceName">Servis adı (Flight, Reservation, Gateway, Identity, Payment, Notification)</param>
    public static void Configure(
        HostBuilderContext context,
        LoggerConfiguration loggerConfig,
        string serviceName)
    {
        var configuration = context.Configuration;
        var seqUrl = configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
        var seqApiKey = configuration["Seq:ApiKey"];

        loggerConfig
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithTraceId()
            .Enrich.WithSpanId()
            .Enrich.With(new ExceptionEnricher());

        if (configuration.GetValue<bool>("Serilog:EnableConsole", true))
        {
            loggerConfig.WriteTo.Console();
        }

        if (configuration.GetValue<bool>("Serilog:EnableSeq", true))
        {
            loggerConfig.WriteTo.Seq(seqUrl, apiKey: seqApiKey);
        }
    }

    /// <summary>
    /// UseSerilog ile kullanım için extension-friendly overload.
    /// builder.Host.UseSerilog(SerilogConfiguration.CreateLogger("Flight"));
    /// </summary>
    public static void CreateLogger(HostBuilderContext context, LoggerConfiguration loggerConfig)
    {
        var serviceName = context.Configuration["Serilog:ServiceName"]
            ?? context.HostingEnvironment.ApplicationName
            ?? "SkySync";
        Configure(context, loggerConfig, serviceName);
    }
}

/// <summary>
/// OpenTelemetry TraceId ve SpanId enricher.
/// Log satırlarında TraceId/SpanId ile Seq'de trace correlation.
/// </summary>
internal static class SerilogEnrichers
{
    public static LoggerConfiguration WithTraceId(this Serilog.Configuration.LoggerEnrichmentConfiguration config)
    {
        return config.With(new TraceIdEnricher());
    }

    public static LoggerConfiguration WithSpanId(this Serilog.Configuration.LoggerEnrichmentConfiguration config)
    {
        return config.With(new SpanIdEnricher());
    }
}

internal sealed class TraceIdEnricher : Serilog.Core.ILogEventEnricher
{
    public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
        }
    }
}

internal sealed class SpanIdEnricher : Serilog.Core.ILogEventEnricher
{
    public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        var spanId = Activity.Current?.SpanId.ToString();
        if (!string.IsNullOrEmpty(spanId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));
        }
    }
}
