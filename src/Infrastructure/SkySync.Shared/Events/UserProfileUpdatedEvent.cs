using System;
using System.Collections.Generic;

namespace SkySync.Shared.Events;

public class UserProfileUpdatedEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> UpdatedFields { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}
