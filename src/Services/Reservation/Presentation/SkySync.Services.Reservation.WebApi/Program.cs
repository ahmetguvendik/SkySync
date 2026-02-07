using System.Collections.Generic;
using System.Reflection;
using Asp.Versioning;
using FluentValidation;
using MassTransit.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SkySync.Infrastructure.Logging;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Reservation.Application.Behaviors;
using SkySync.Services.Reservation.Application.Validators;
using SkySync.Services.Reservation.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => SerilogConfiguration.Configure(ctx, lc, "Reservation"));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests.CreateReservationCommandRequest).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateReservationCommandRequestValidator>();
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Add Persistence Services
builder.Services.AddPersistenceService(builder.Configuration);

// Add MassTransit with RabbitMQ and Saga State Machine
builder.Services.AddMassTransitService(builder.Configuration);

// Eureka Service Discovery - Register with Eureka
builder.Services.AddEurekaDiscoveryClient();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration["OpenTelemetry:ServiceName"] ?? "SkySync-Reservation",
            serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object> { ["deployment.environment"] = builder.Environment.EnvironmentName }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(o =>
            {
                o.EnrichWithHttpRequest = (activity, httpRequest) =>
                {
                    if (httpRequest.HttpContext != null)
                        OpenTelemetryUserEnrichment.EnrichActivityWithUser(activity, httpRequest.HttpContext);
                };
            })
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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCorrelationIdLogContext();
app.UseUserLogContext();

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

app.UseAuthorization();

// Tutarlı hata response: message, code (opsiyonel), errors (validasyon)
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var errors = validationException.Errors.Select(e => new { propertyName = e.PropertyName, errorMessage = e.ErrorMessage }).ToList();
            await context.Response.WriteAsJsonAsync(new { message = "Validasyon hatası.", errors });
        }
        else if (exception is KeyNotFoundException keyEx)
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