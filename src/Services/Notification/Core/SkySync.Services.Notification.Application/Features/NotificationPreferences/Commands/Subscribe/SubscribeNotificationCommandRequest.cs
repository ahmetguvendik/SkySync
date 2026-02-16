using System;
using MediatR;

namespace SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Subscribe;

public class SubscribeNotificationCommandRequest : IRequest<SubscribeNotificationCommandResponse>
{
    public Guid UserId { get; set; }
}
