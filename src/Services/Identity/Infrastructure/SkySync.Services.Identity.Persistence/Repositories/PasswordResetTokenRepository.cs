using Microsoft.EntityFrameworkCore;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Services.Identity.Persistence.Contexts;

namespace SkySync.Services.Identity.Persistence.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly IdentityServiceDbContext _context;

    public PasswordResetTokenRepository(IdentityServiceDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordResetToken> CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        await _context.PasswordResetTokens.AddAsync(token, cancellationToken);
        return token;
    }

    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.Token == token && !x.IsDeleted, cancellationToken);
    }

    public async Task InvalidateUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await _context.PasswordResetTokens
            .Where(x => x.UserId == userId && !x.IsDeleted && !x.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsUsed = true;
            token.UsedAt = now;
            token.ModifiedTime = now;
        }
    }
}
