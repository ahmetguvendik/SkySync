using System.Collections.Generic;
using AspNetCoreRateLimit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Steeltoe.Discovery.Eureka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SkySync.Gateway.Configuration;
using SkySync.Gateway.Health;
using SkySync.Infrastructure.Logging;
using SkySync.Gateway.Middleware;
using SkySync.Gateway.Resilience;
using Yarp.ReverseProxy.Forwarder;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------- SERILOG --------------------
builder.Host.UseSerilog((ctx, lc) => SerilogConfiguration.Configure(ctx, lc, "Gateway"));

// -------------------- CORE --------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// -------------------- YARP --------------------
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// -------------------- POLLY RESILIENCE --------------------
builder.Services.Configure<PollyResilienceOptions>(
    builder.Configuration.GetSection(PollyResilienceOptions.SectionName));
builder.Services.AddSingleton<CircuitBreakerStateStore>();
builder.Services.AddSingleton<PollyResilienceForwarderHttpClientFactory>();
builder.Services.AddSingleton<IForwarderHttpClientFactory>(sp =>
    sp.GetRequiredService<PollyResilienceForwarderHttpClientFactory>());

// -------------------- JWT --------------------
var secretKey = JwtConfiguration.GetSecretKey(builder.Configuration);
var issuer = JwtConfiguration.GetIssuer(builder.Configuration);
var audience = JwtConfiguration.GetAudience(builder.Configuration);

if (secretKey.Length < 32)
    throw new InvalidOperationException("JWT Secret Key must be at least 32 characters long!");

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Public", policy => policy.RequireAssertion(_ => true));
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

// -------------------- CORS --------------------
CorsConfiguration.AddCorsPolicies(builder.Services, builder.Configuration);

// -------------------- RATE LIMIT --------------------
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(
    builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// -------------------- HEALTH --------------------
builder.Services.AddHealthChecks()
    .AddCheck<CircuitBreakerHealthCheck>("circuit_breaker", tags: ["ready", "live"])
    .AddCheck<SeqHealthCheck>("seq", tags: ["ready", "live"]);

// -------------------- EUREKA --------------------
builder.Services.AddEurekaDiscoveryClient();

// -------------------- OPENTELEMETRY --------------------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration["OpenTelemetry:ServiceName"] ?? "SkySync-Gateway",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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
            .AddOtlpExporter(options =>
            {
                var endpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
                options.Endpoint = new Uri(endpoint);
            });
    });

var app = builder.Build();

// -------------------- PIPELINE --------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

app.UseCorrelationId();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
        diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
        var correlationId = httpContext.Items["CorrelationId"]?.ToString();
        var transactionId = httpContext.Items["TransactionId"]?.ToString();
        if (!string.IsNullOrEmpty(correlationId)) diagnosticContext.Set("CorrelationId", correlationId);
        if (!string.IsNullOrEmpty(transactionId)) diagnosticContext.Set("TransactionId", transactionId);
    };
});
app.UseMiddleware<RequestTransformationMiddleware>();

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();
app.UseUserLogContext();

app.MapHealthChecks("/health");

// -------------------- REVERSE PROXY --------------------
app.MapReverseProxy();

// -------------------- INFO --------------------
app.MapGet("/", () => new
{
    Service = "SkySync API Gateway",
    Status = "Running",
    Environment = app.Environment.EnvironmentName,
Routes = new[]
        {
            "/api/v1/auth",
            "/api/v1/flight",
            "/api/v1/reservation",
            "/api/v1/payment",
            "/api/v1/notification"
        }
}).AllowAnonymous();

app.Run();
