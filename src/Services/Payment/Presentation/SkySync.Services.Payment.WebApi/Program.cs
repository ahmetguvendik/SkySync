using System.Collections.Generic;
using System.Reflection;
using Microsoft.OpenApi.Models;
using MassTransit.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Payment.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ServiceName", "Payment")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

// Add Persistence & DB
builder.Services.AddPersistenceServices(builder.Configuration);

// Add MassTransit with Consumers
builder.Services.AddMassTransitService(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkySync Payment API", Version = "v1" });
});
builder.Services.AddHealthChecks();

// Eureka Service Discovery - Register with Eureka
builder.Services.AddEurekaDiscoveryClient();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration["OpenTelemetry:ServiceName"] ?? "SkySync-Payment",
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkySync Payment API v1"));
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
