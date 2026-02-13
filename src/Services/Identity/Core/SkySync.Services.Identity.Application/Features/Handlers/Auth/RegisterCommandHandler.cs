using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Domain.Constants;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class RegisterCommandHandler : IRequestHandler<RegisterCommandRequest, RegisterCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IEmailVerificationTokenRepository _emailVerificationTokenRepository;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOutboxRepository outboxRepository,
        IEmailVerificationTokenRepository emailVerificationTokenRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _outboxRepository = outboxRepository;
        _emailVerificationTokenRepository = emailVerificationTokenRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RegisterCommandResponse> Handle(RegisterCommandRequest request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return new RegisterCommandResponse
            {
                IsSuccess = false,
                Message = "Bu email adresi zaten kayıtlı."
            };
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            RoleId = RoleConstants.UserRoleId,
            IsEmailConfirmed = false,
            CreatedTime = now,
            ModifiedTime = now,
            IsDeleted = false
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.CreateAsync(user, cancellationToken);

            await _emailVerificationTokenRepository.InvalidateTokensAsync(user.Id, cancellationToken);
            var tokenValue = GenerateSecureToken();
            var expiresAt = now.AddMinutes(
                _configuration.GetValue<int>("EmailVerification:TokenExpirationMinutes", 60));
            var verificationLinkBase = _configuration["EmailVerification:VerificationLinkBaseUrl"]
                ?? "https://app.skysync.com/verify-email";
            var verificationLink = BuildVerificationLink(verificationLinkBase, tokenValue);

            var verificationToken = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = tokenValue,
                ExpiresAt = expiresAt,
                IsUsed = false,
                CreatedTime = now,
                ModifiedTime = now,
                IsDeleted = false
            };

            await _emailVerificationTokenRepository.CreateAsync(verificationToken, cancellationToken);

            var verificationEvent = new EmailVerificationRequestedEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                VerificationToken = tokenValue,
                ExpiresAt = expiresAt,
                VerificationLink = verificationLink
            };

            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(EmailVerificationRequestedEvent),
                Content = JsonSerializer.Serialize(verificationEvent),
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
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Kullanıcı kaydı sırasında hata oluştu. Email: {Email}", request.Email);
            throw;
        }

        return new RegisterCommandResponse
        {
            IsSuccess = true,
            Message = "Kayıt başarılı. Lütfen email adresinizi doğrulayın.",
            UserId = user.Id
        };
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string BuildVerificationLink(string baseUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return token;
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
