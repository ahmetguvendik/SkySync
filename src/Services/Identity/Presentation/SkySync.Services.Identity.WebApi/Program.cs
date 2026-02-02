using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Steeltoe.Discovery.Eureka;
using Microsoft.IdentityModel.Tokens;
using SkySync.Services.Identity.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "SkySync Identity API", Version = "v1" });
});
builder.Services.AddHealthChecks();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SkySync.Services.Identity.Application.Features.Commands.Auth.Requests.RegisterCommandRequest).Assembly));

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkySync Identity API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
