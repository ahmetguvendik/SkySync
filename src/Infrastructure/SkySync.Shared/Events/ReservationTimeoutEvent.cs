namespace SkySync.Shared.Events;

/// <summary>
/// Saga timeout event - Genel timeout (herhangi bir adımda takılırsa)
/// </summary>
public class ReservationTimeoutEvent
{
    public Guid CorrelationId { get; set; }
    public DateTime TimeoutAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "General reservation timeout";
}
