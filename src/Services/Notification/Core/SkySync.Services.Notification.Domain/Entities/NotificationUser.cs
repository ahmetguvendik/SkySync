namespace SkySync.Services.Notification.Domain.Entities;

/// <summary>
/// Notification servisinin takip ettiği kullanıcı read modeli.
/// Identity'den gelen event'ler ile beslenir.
/// </summary>
public class NotificationUser
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool ReceivesOperationalEmails { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public Guid UnsubscribeToken { get; set; }
}
