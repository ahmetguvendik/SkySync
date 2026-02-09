using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using System.Diagnostics;
using System.Text.Json;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class RegisterCommandHandler : IRequestHandler<RegisterCommandRequest, RegisterCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
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
            Role = "User",
            CreatedTime = now,
            ModifiedTime = now,
            IsDeleted = false
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.CreateAsync(user, cancellationToken);

            var userRegisteredEvent = new UserRegisteredEvent
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
                Content = JsonSerializer.Serialize(userRegisteredEvent),
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
            Message = "Kayıt başarılı.",
            UserId = user.Id
        };
    }
}
