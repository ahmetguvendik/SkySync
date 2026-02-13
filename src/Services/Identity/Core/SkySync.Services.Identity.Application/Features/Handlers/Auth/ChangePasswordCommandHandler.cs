using System;
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

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommandRequest, ChangePasswordCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ChangePasswordCommandResponse> Handle(ChangePasswordCommandRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return new ChangePasswordCommandResponse
            {
                IsSuccess = false,
                Message = "Kullanıcı bulunamadı."
            };
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return new ChangePasswordCommandResponse
            {
                IsSuccess = false,
                Message = "Mevcut şifre hatalı."
            };
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.UpdateAsync(user, cancellationToken);

            var passwordChangedEvent = new UserPasswordChangedEvent
            {
                UserId = user.Id,
                Email = user.Email,
                ChangedAt = now
            };

            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(UserPasswordChangedEvent),
                Content = JsonSerializer.Serialize(passwordChangedEvent),
                OccurredOn = now,
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
            _logger.LogError(ex, "Şifre değişimi sırasında hata oluştu. UserId: {UserId}", request.UserId);
            throw;
        }

        _logger.LogInformation("Kullanıcı şifresi güncellendi. UserId: {UserId}", request.UserId);

        return new ChangePasswordCommandResponse
        {
            IsSuccess = true,
            Message = "Şifreniz güncellendi."
        };
    }
}
