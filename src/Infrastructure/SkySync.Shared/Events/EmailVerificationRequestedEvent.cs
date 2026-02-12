namespace SkySync.Shared.Events;

public class EmailVerificationRequestedEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string VerificationLink { get; set; } = string.Empty;
}
