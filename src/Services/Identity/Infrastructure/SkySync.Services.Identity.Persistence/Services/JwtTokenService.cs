using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SkySync.Services.Identity.Application.Interfaces;

namespace SkySync.Services.Identity.Persistence.Services;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Guid userId, string email, string role, IEnumerable<Claim>? additionalClaims = null)
    {
        var secretKey = GetSecretKey();
        if (secretKey.Length < 32)
            throw new InvalidOperationException("JWT Secret Key must be at least 32 characters long.");

        var issuer = _configuration["JwtSettings:Issuer"] ?? "SkySync";
        var audience = _configuration["JwtSettings:Audience"] ?? "SkySyncUsers";
        var expiryStr = _configuration["JwtSettings:ExpirationMinutes"];
        var expiryMinutes = int.TryParse(expiryStr, out var em) ? em : 60;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };

        if (additionalClaims != null)
            claims.AddRange(additionalClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GetSecretKey()
    {
        var key = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                  ?? _configuration["JwtSettings:SecretKey"];
        return key ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
    }
}
