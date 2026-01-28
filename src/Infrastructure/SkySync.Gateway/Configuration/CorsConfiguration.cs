namespace SkySync.Gateway.Configuration;

/// <summary>
/// CORS Configuration - Production Safe
/// </summary>
public static class CorsConfiguration
{
    public static void AddCorsPolicies(IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            // Development: Allow All
            options.AddPolicy("Development", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });

            // Production: Restricted
            var allowedOrigins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() 
                                 ?? new[] { "https://skysync.com" };

            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });
    }
}
