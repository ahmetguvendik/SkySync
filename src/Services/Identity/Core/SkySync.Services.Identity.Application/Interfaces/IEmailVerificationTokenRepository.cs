using SkySync.Services.Identity.Domain.Entities;

namespace SkySync.Services.Identity.Application.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken> CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);
    Task<EmailVerificationToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task InvalidateTokensAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsUsedAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
