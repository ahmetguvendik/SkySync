using System;
using MediatR;

namespace SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Unsubscribe;

public class UnsubscribeNotificationCommandRequest : IRequest<UnsubscribeNotificationCommandResponse>
{
    public Guid Token { get; set; }
}
