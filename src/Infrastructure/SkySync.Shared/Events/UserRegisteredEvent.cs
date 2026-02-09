namespace SkySync.Shared.Events;

/// <summary>
/// Identity servisi yeni kullanıcı oluşturduğunda publish edilecek event
/// Notification servisi tarafından consume edilir ve hoş geldin maili gönderilir.
/// </summary>
public class UserRegisteredEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
