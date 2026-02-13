using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Services.Identity.Domain.Constants;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using System;
using System.Diagnostics;
using System.Text.Json;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class CreateAdminCommandHandler : IRequestHandler<CreateAdminCommandRequest, RegisterCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAdminCommandHandler> _logger;

    public CreateAdminCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateAdminCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RegisterCommandResponse> Handle(CreateAdminCommandRequest request, CancellationToken cancellationToken)
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
            RoleId = RoleConstants.AdminRoleId,
            IsEmailConfirmed = true,
            CreatedTime = now,
            ModifiedTime = now,
            IsDeleted = false
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.CreateAsync(user, cancellationToken);

            var welcomeEvent = new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RegisteredAt = now
            };

            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(UserRegisteredEvent),
                Content = JsonSerializer.Serialize(welcomeEvent),
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
            _logger.LogError(ex, "Admin kullanıcısı oluşturulurken hata oluştu. Email: {Email}", request.Email);
            throw;
        }

        _logger.LogInformation("Admin kullanıcı oluşturuldu. Email: {Email}", request.Email);

        return new RegisterCommandResponse
        {
            IsSuccess = true,
            Message = "Admin kullanıcısı oluşturuldu.",
            UserId = user.Id
        };
    }
}
