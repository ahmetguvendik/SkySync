namespace SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Subscribe;

public class SubscribeNotificationCommandResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsNotFound { get; set; }
}
