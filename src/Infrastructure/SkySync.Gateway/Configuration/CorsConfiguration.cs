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
            // Development: Allow localhost origins with credentials
            options.AddPolicy("Development", policy =>
            {
                policy.WithOrigins(
                        "http://localhost:5173",  // Vite/React default
                        "http://localhost:3000",  // React/Next.js default
                        "http://localhost:4200",  // Angular default
                        "http://localhost:8080"  // Vue default
                        //  // localtunnel frontend
                      )
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
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
