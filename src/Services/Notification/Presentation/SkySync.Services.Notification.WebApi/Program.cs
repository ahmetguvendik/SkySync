using System.Reflection;
using System.Text;
using Asp.Versioning;
using MassTransit.Logging;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SkySync.Infrastructure.Logging;
using SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Unsubscribe;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Notification.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => SerilogConfiguration.Configure(ctx, lc, "Notification"));

// Add Notification Services (DI + MassTransit)
builder.Services.AddNotificationServices(builder.Configuration);
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UnsubscribeNotificationCommandRequest).Assembly));
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var secretKey = builder.Configuration["JwtSettings:SecretKey"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLongForProduction!";
var issuer = builder.Configuration["JwtSettings:Issuer"] ?? "SkySync";
var audience = builder.Configuration["JwtSettings:Audience"] ?? "SkySyncUsers";

if (secretKey.Length < 32)
    throw new InvalidOperationException("JWT Secret Key must be at least 32 characters long.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });
builder.Services.AddAuthorization();

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

app.UseCorrelationIdLogContext();

app.UseAuthentication();
app.UseAuthorization();

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
