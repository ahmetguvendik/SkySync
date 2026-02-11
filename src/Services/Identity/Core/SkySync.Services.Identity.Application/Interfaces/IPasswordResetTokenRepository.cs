using SkySync.Services.Identity.Domain.Entities;

namespace SkySync.Services.Identity.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken> CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task InvalidateUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
