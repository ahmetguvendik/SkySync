using System;

namespace SkySync.Shared.Events;

public class UserPasswordChangedEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
