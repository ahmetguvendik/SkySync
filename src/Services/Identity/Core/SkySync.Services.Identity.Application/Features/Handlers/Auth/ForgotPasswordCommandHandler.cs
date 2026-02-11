using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommandRequest, ForgotPasswordCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ForgotPasswordCommandResponse> Handle(ForgotPasswordCommandRequest request, CancellationToken cancellationToken)
    {
        var genericMessage = "Eğer e-posta sistemimizde kayıtlıysa şifre sıfırlama bağlantısı gönderildi.";
        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            _logger.LogWarning("Şifre sıfırlama isteği - boş veya geçersiz e-posta değeri alındı.");
            return new ForgotPasswordCommandResponse { IsSuccess = true, Message = genericMessage };
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
        {
            _logger.LogInformation("Şifre sıfırlama isteği - kullanıcı bulunamadı. Email: {Email}", normalizedEmail);
            return new ForgotPasswordCommandResponse { IsSuccess = true, Message = genericMessage };
        }

        var now = DateTime.UtcNow;
        var expirationMinutesSetting = _configuration["PasswordReset:TokenExpirationMinutes"];
        var expirationMinutes = int.TryParse(expirationMinutesSetting, out var parsedMinutes) && parsedMinutes > 0
            ? parsedMinutes
            : 30;
        var expiresAt = now.AddMinutes(expirationMinutes);

        var resetLinkBaseUrl = _configuration["PasswordReset:ResetLinkBaseUrl"] ?? "https://app.skysync.com/reset-password";
        var resetTokenValue = GenerateSecureToken();
        var resetLink = BuildResetLink(resetLinkBaseUrl, resetTokenValue);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _passwordResetTokenRepository.InvalidateUserTokensAsync(user.Id, cancellationToken);

            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = resetTokenValue,
                ExpiresAt = expiresAt,
                IsUsed = false,
                CreatedTime = now,
                ModifiedTime = now,
                IsDeleted = false
            };

            await _passwordResetTokenRepository.CreateAsync(resetToken, cancellationToken);

            var resetEvent = new PasswordResetRequestedEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Token = resetToken.Token,
                ExpiresAt = resetToken.ExpiresAt,
                ResetLink = resetLink,
                RequestedAt = now
            };

            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(PasswordResetRequestedEvent),
                Content = JsonSerializer.Serialize(resetEvent),
                OccurredOn = now,
                ProcessedOn = null,
                Error = null,
                RetryCount = 0,
                IsFailed = false,
                Traceparent = activity?.Id,
                Tracestate = activity?.TraceStateString
            };

            await _outboxRepository.CreateAsync(outboxMessage, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Şifre sıfırlama isteği oluşturuldu. UserId: {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Şifre sıfırlama isteği oluşturulurken hata oluştu. Email: {Email}", normalizedEmail);
            throw;
        }

        return new ForgotPasswordCommandResponse
        {
            IsSuccess = true,
            Message = genericMessage
        };
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string BuildResetLink(string baseUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return token;
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
