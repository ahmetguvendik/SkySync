using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;

namespace SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Subscribe;

public class SubscribeNotificationCommandHandler
    : IRequestHandler<SubscribeNotificationCommandRequest, SubscribeNotificationCommandResponse>
{
    private readonly INotificationUserRepository _notificationUserRepository;
    private readonly ILogger<SubscribeNotificationCommandHandler> _logger;

    public SubscribeNotificationCommandHandler(
        INotificationUserRepository notificationUserRepository,
        ILogger<SubscribeNotificationCommandHandler> logger)
    {
        _notificationUserRepository = notificationUserRepository;
        _logger = logger;
    }

    public async Task<SubscribeNotificationCommandResponse> Handle(
        SubscribeNotificationCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return new SubscribeNotificationCommandResponse
            {
                IsSuccess = false,
                Message = "Geçersiz kullanıcı bilgisi."
            };
        }

        var updated = await _notificationUserRepository.UpdateOperationalPreferenceAsync(
            request.UserId,
            true,
            cancellationToken);

        if (!updated)
        {
            return new SubscribeNotificationCommandResponse
            {
                IsSuccess = false,
                Message = "Kullanıcı bulunamadı.",
                IsNotFound = true
            };
        }

        await _notificationUserRepository.RegenerateUnsubscribeTokenAsync(request.UserId, cancellationToken);
        _logger.LogInformation("User resubscribed to operational emails. UserId: {UserId}", request.UserId);

        return new SubscribeNotificationCommandResponse
        {
            IsSuccess = true,
            Message = "Bildirimler yeniden açıldı."
        };
    }
}
