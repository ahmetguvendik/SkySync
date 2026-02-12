using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using System.Diagnostics;
using System.Text.Json;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommandRequest, VerifyEmailCommandResponse>
{
    private readonly IEmailVerificationTokenRepository _emailVerificationTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VerifyEmailCommandHandler> _logger;

    public VerifyEmailCommandHandler(
        IEmailVerificationTokenRepository emailVerificationTokenRepository,
        IUserRepository userRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<VerifyEmailCommandHandler> logger)
    {
        _emailVerificationTokenRepository = emailVerificationTokenRepository;
        _userRepository = userRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<VerifyEmailCommandResponse> Handle(VerifyEmailCommandRequest request, CancellationToken cancellationToken)
    {
        var tokenValue = (request.Token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return new VerifyEmailCommandResponse
            {
                IsSuccess = false,
                Message = "Doğrulama tokenı geçersiz."
            };
        }

        var token = await _emailVerificationTokenRepository.GetByTokenAsync(tokenValue, cancellationToken);
        if (token == null || token.IsDeleted || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Invalid email verification token used.");
            return new VerifyEmailCommandResponse
            {
                IsSuccess = false,
                Message = "Doğrulama bağlantısı geçersiz veya süresi dolmuş."
            };
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
            if (user == null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new VerifyEmailCommandResponse
                {
                    IsSuccess = false,
                    Message = "Kullanıcı bulunamadı."
                };
            }

            if (!user.IsEmailConfirmed)
            {
                user.IsEmailConfirmed = true;
                await _userRepository.UpdateAsync(user, cancellationToken);
            }

            await _emailVerificationTokenRepository.MarkAsUsedAsync(token.Id, cancellationToken);

            var welcomeEvent = new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RegisteredAt = DateTime.UtcNow
            };

            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(UserRegisteredEvent),
                Content = JsonSerializer.Serialize(welcomeEvent),
                OccurredOn = DateTime.UtcNow,
                Traceparent = activity?.Id,
                Tracestate = activity?.TraceStateString
            };

            await _outboxRepository.CreateAsync(outboxMessage, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Email verified successfully. UserId: {UserId}", user.Id);

            return new VerifyEmailCommandResponse
            {
                IsSuccess = true,
                Message = "Email adresiniz doğrulandı."
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Error verifying email token");
            throw;
        }
    }
}
