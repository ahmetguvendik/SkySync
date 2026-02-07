using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FluentValidation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Identity.Application.Behaviors;
using SkySync.Services.Identity.Application.Validators;
using SkySync.Services.Identity.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ServiceName", "Identity")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "SkySync Identity API", Version = "v1" });
});
builder.Services.AddHealthChecks();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SkySync.Services.Identity.Application.Features.Commands.Auth.Requests.RegisterCommandRequest).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<RegisterCommandRequestValidator>();
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddPersistenceServices(builder.Configuration);

// JWT - Profile endpoint için (Gateway ile aynı secret/issuer/audience kullanılmalı)
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

builder.Services.AddEurekaDiscoveryClient();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration["OpenTelemetry:ServiceName"] ?? "SkySync-Identity",
            serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object> { ["deployment.environment"] = builder.Environment.EnvironmentName }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
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
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkySync Identity API v1"));
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
app.UseAuthentication();
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
