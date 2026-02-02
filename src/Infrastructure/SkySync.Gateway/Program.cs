using AspNetCoreRateLimit;
using Steeltoe.Discovery.Eureka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SkySync.Gateway.Configuration;
using SkySync.Gateway.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------- CORE --------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// -------------------- YARP --------------------
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

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
builder.Services.AddHealthChecks();

// -------------------- EUREKA --------------------
builder.Services.AddEurekaDiscoveryClient();

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
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RequestTransformationMiddleware>();

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

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
        "/api/auth",
        "/api/flight",
        "/api/reservation",
        "/api/payment",
        "/api/notification"
    }
}).AllowAnonymous();

app.Run();
