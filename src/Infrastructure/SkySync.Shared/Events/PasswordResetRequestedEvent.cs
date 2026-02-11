namespace SkySync.Shared.Events;

/// <summary>
/// Identity servisinde bir kullanıcı şifremi unuttum dediğinde yayınlanacak event.
/// Notification servisi mail ile sıfırlama bağlantısını gönderir.
/// </summary>
public class PasswordResetRequestedEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
