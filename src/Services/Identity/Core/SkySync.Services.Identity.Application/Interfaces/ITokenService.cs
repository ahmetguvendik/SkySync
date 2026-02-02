using System.Security.Claims;

namespace SkySync.Services.Identity.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email, string role, IEnumerable<Claim>? additionalClaims = null);
}
