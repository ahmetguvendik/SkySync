namespace SkySync.Shared.Events;

/// <summary>
/// Ödeme 5 dk içinde gelmedi (timeout). Reservation Failed yapılsın.
/// </summary>
public class ReservationTimedOutEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public string Reason { get; set; } = "Ödeme 5 dakika içinde tamamlanmadı.";
    public DateTime TimedOutAt { get; set; } = DateTime.UtcNow;
}
