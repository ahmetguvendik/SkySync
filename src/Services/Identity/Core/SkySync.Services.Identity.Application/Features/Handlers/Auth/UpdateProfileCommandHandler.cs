using System;
using System.Collections.Generic;
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

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommandRequest, UpdateProfileCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateProfileCommandHandler> _logger;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateProfileCommandHandler> logger)
    {
        _userRepository = userRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UpdateProfileCommandResponse> Handle(UpdateProfileCommandRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return new UpdateProfileCommandResponse
            {
                IsSuccess = false,
                Message = "Kullanıcı bulunamadı."
            };
        }

        var updatedFields = new List<string>();
        var now = DateTime.UtcNow;

        if (!string.Equals(user.FirstName, request.FirstName, StringComparison.Ordinal))
        {
            user.FirstName = request.FirstName;
            updatedFields.Add("FirstName");
        }

        if (!string.Equals(user.LastName, request.LastName, StringComparison.Ordinal))
        {
            user.LastName = request.LastName;
            updatedFields.Add("LastName");
        }

        var normalizedEmail = request.Email.ToLowerInvariant();
        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userRepository.ExistsByEmailExceptIdAsync(normalizedEmail, user.Id, cancellationToken))
            {
                return new UpdateProfileCommandResponse
                {
                    IsSuccess = false,
                    Message = "Bu e-posta adresi başka bir kullanıcı tarafından kullanılıyor."
                };
            }

            user.Email = normalizedEmail;
            updatedFields.Add("Email");
        }

        if (updatedFields.Count == 0)
        {
            return new UpdateProfileCommandResponse
            {
                IsSuccess = true,
                Message = "Herhangi bir değişiklik yapılmadı."
            };
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.UpdateAsync(user, cancellationToken);

            var profileUpdatedEvent = new UserProfileUpdatedEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UpdatedFields = updatedFields,
                UpdatedAt = now
            };

            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(UserProfileUpdatedEvent),
                Content = JsonSerializer.Serialize(profileUpdatedEvent),
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
            _logger.LogError(ex, "Profil güncelleme sırasında hata oluştu. UserId: {UserId}", request.UserId);
            throw;
        }

        _logger.LogInformation("Kullanıcı profili güncellendi. UserId: {UserId}", request.UserId);

        return new UpdateProfileCommandResponse
        {
            IsSuccess = true,
            Message = "Profiliniz güncellendi."
        };
    }
}
