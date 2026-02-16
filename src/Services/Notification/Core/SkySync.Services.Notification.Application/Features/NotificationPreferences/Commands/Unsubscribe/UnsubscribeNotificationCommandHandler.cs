using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;

namespace SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Unsubscribe;

public class UnsubscribeNotificationCommandHandler
    : IRequestHandler<UnsubscribeNotificationCommandRequest, UnsubscribeNotificationCommandResponse>
{
    private readonly INotificationUserRepository _notificationUserRepository;
    private readonly ILogger<UnsubscribeNotificationCommandHandler> _logger;

    public UnsubscribeNotificationCommandHandler(
        INotificationUserRepository notificationUserRepository,
        ILogger<UnsubscribeNotificationCommandHandler> logger)
    {
        _notificationUserRepository = notificationUserRepository;
        _logger = logger;
    }

    public async Task<UnsubscribeNotificationCommandResponse> Handle(
        UnsubscribeNotificationCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Token == Guid.Empty)
        {
            return new UnsubscribeNotificationCommandResponse
            {
                IsSuccess = false,
                Message = "Token geçersiz."
            };
        }

        var user = await _notificationUserRepository.GetByUnsubscribeTokenAsync(request.Token, cancellationToken);
        if (user == null)
        {
            return new UnsubscribeNotificationCommandResponse
            {
                IsSuccess = false,
                Message = "Token geçersiz veya kullanıcı bulunamadı."
            };
        }

        await _notificationUserRepository.UpdateOperationalPreferenceAsync(user.UserId, false, cancellationToken);
        await _notificationUserRepository.RegenerateUnsubscribeTokenAsync(user.UserId, cancellationToken);

        _logger.LogInformation("User unsubscribed via token. UserId: {UserId}", user.UserId);

        return new UnsubscribeNotificationCommandResponse
        {
            IsSuccess = true,
            Message = "Bildirimler başarıyla kapatıldı.",
            Email = user.Email
        };
    }
}
