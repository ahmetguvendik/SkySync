using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Application.UnitOfWorks;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommandRequest, ResetPasswordCommandResponse>
{
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ResetPasswordCommandResponse> Handle(ResetPasswordCommandRequest request, CancellationToken cancellationToken)
    {
        var tokenValue = (request.Token ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            _logger.LogWarning("Boş veya geçersiz bir şifre sıfırlama token'ı alındı.");
            return new ResetPasswordCommandResponse
            {
                IsSuccess = false,
                Message = "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş."
            };
        }
        var token = await _passwordResetTokenRepository.GetByTokenAsync(tokenValue, cancellationToken);

        if (token == null || token.IsDeleted || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Geçersiz veya süresi dolmuş şifre sıfırlama token'ı kullanıldı.");
            return new ResetPasswordCommandResponse
            {
                IsSuccess = false,
                Message = "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş."
            };
        }

        var now = DateTime.UtcNow;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
            var updated = await _userRepository.UpdatePasswordHashAsync(token.UserId, newPasswordHash, cancellationToken);

            if (!updated)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogWarning("Şifre sıfırlama sırasında kullanıcı bulunamadı. TokenId: {TokenId}", token.Id);
                return new ResetPasswordCommandResponse
                {
                    IsSuccess = false,
                    Message = "Şifre güncellenemedi. Lütfen yeni bir bağlantı isteyin."
                };
            }

            token.IsUsed = true;
            token.UsedAt = now;
            token.ModifiedTime = now;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Şifre sıfırlama başarılı. UserId: {UserId}", token.UserId);

            return new ResetPasswordCommandResponse
            {
                IsSuccess = true,
                Message = "Şifreniz başarıyla güncellendi."
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Şifre sıfırlama sırasında hata oluştu. TokenId: {TokenId}", token.Id);
            throw;
        }
    }
}
