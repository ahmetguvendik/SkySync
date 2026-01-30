using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SkySync.Gateway.Configuration;
using SkySync.Gateway.Middleware;
using SkySync.Gateway.Resilience;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add YARP Reverse Proxy with Circuit Breaker
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        // Request/Response transformation için
    });

// 1. JWT AUTHENTICATION - Environment Variable Support
var secretKey = JwtConfiguration.GetSecretKey(builder.Configuration);
var issuer = JwtConfiguration.GetIssuer(builder.Configuration);
var audience = JwtConfiguration.GetAudience(builder.Configuration);

if (secretKey.Length < 32)
{
    throw new InvalidOperationException("JWT Secret Key must be at least 32 characters long!");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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

// Add Authorization
builder.Services.AddAuthorization(options =>
{
    // Public endpoints (no auth required)
    options.AddPolicy("Public", policy => policy.RequireAssertion(_ => true));
    
    // Authenticated endpoints
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
    
    // Admin endpoints
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// 2. CORS - Production Safe
CorsConfiguration.AddCorsPolicies(builder.Services, builder.Configuration);

// 3. RATE LIMITING - AspNetCoreRateLimit
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// 4. RESILIENCE POLICIES - Timeout + Retry + Circuit Breaker
// YARP built-in timeout ve circuit breaker kullanılıyor (appsettings.json'da config edildi)
// - Timeout: HttpRequest.Timeout (30 saniye) - appsettings.json'da ayarlandı
// - Circuit Breaker: MaxConcurrentRequests, MaxRequestsPerQueue - appsettings.json'da ayarlandı
// - Retry: YARP otomatik retry yapmaz, gerekirse Polly ile eklenebilir
builder.Services.AddHttpClient();

// Add Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS Redirect - Only in Production (prevents CORS preflight issues in dev)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// MIDDLEWARE ORDER IS CRITICAL!

// 1. CORS - MUST BE EARLY (before other middleware that might reject preflight)
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

// 2. Correlation ID Middleware (creates IDs for all downstream)
app.UseCorrelationId();

// 3. Request Logging (uses IDs from CorrelationIdMiddleware)
app.UseMiddleware<RequestLoggingMiddleware>();

// 4. Request/Response Transformation
app.UseMiddleware<RequestTransformationMiddleware>();

// 3. RATE LIMITING
app.UseIpRateLimiting();

// Authentication & Authorization - GEÇİCİ OLARAK KAPALI
// app.UseAuthentication();
// app.UseAuthorization();

// Health Check endpoint
app.MapHealthChecks("/health");

// YARP Reverse Proxy
app.MapReverseProxy();

// API Gateway Info endpoint
app.MapGet("/", () => new
{
    Service = "SkySync API Gateway",
    Version = "1.0.0",
    Status = "Running",
    Environment = app.Environment.EnvironmentName,
    Endpoints = new
    {
        Flight = "/api/flight",
        Reservation = "/api/reservation",
        Payment = "/api/payment",
        Notification = "/api/notification"
    }
}).WithName("GatewayInfo").AllowAnonymous();

app.Run();
