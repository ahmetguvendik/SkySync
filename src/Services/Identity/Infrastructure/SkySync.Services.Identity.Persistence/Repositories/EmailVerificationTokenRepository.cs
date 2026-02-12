using Microsoft.EntityFrameworkCore;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Services.Identity.Persistence.Contexts;

namespace SkySync.Services.Identity.Persistence.Repositories;

public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly IdentityServiceDbContext _context;

    public EmailVerificationTokenRepository(IdentityServiceDbContext context)
    {
        _context = context;
    }

    public async Task<EmailVerificationToken> CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
    {
        await _context.EmailVerificationTokens.AddAsync(token, cancellationToken);
        return token;
    }

    public async Task<EmailVerificationToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.EmailVerificationTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
    }

    public async Task InvalidateTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsUsed = true;
            token.UsedAt = DateTime.UtcNow;
            token.ModifiedTime = DateTime.UtcNow;
        }
    }

    public async Task<bool> MarkAsUsedAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await _context.EmailVerificationTokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (token == null) return false;

        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;
        token.ModifiedTime = DateTime.UtcNow;
        return true;
    }
}
