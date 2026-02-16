namespace SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Unsubscribe;

public class UnsubscribeNotificationCommandResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Email { get; set; }
}
