namespace SkySync.Gateway.Configuration;

/// <summary>
/// JWT Configuration Helper - Environment Variable Support
/// </summary>
public static class JwtConfiguration
{
    public static string GetSecretKey(IConfiguration configuration)
    {
        // 1. Önce Environment Variable'dan dene (Production)
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        if (!string.IsNullOrEmpty(secretKey))
        {
            return secretKey;
        }

        // 2. Sonra appsettings.json'dan al (Development)
        secretKey = configuration["JwtSettings:SecretKey"];

        if (!string.IsNullOrEmpty(secretKey))
        {
            return secretKey;
        }

        // 3. Fallback (sadece development için)
        return "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
    }

    public static string GetIssuer(IConfiguration configuration)
    {
        return Environment.GetEnvironmentVariable("JWT_ISSUER")
               ?? configuration["JwtSettings:Issuer"]
               ?? "SkySync";
    }

    public static string GetAudience(IConfiguration configuration)
    {
        return Environment.GetEnvironmentVariable("JWT_AUDIENCE")
               ?? configuration["JwtSettings:Audience"]
               ?? "SkySyncUsers";
    }
}
