using System.Collections.Generic;
using System.Reflection;
using MassTransit.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Notification.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ServiceName", "Notification")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

// Add Notification Services (DI + MassTransit)
builder.Services.AddNotificationServices(builder.Configuration);

// Eureka Service Discovery - Register with Eureka
builder.Services.AddEurekaDiscoveryClient();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration["OpenTelemetry:ServiceName"] ?? "SkySync-Notification",
            serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object> { ["deployment.environment"] = builder.Environment.EnvironmentName }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource(DiagnosticHeaders.DefaultListenerName)  // MassTransit mesaj trace
            .AddOtlpExporter(options =>
            {
                var endpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
                options.Endpoint = new Uri(endpoint);
            });
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
        diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
    };
});

// Tutarlı hata response: message, code (tüm servislerle aynı format)
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is KeyNotFoundException keyEx)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = keyEx.Message ?? "Kayıt bulunamadı.", code = "NOT_FOUND" });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    });
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
