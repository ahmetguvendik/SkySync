namespace SkySync.Shared.Events;

/// <summary>
/// Flight Service'den gelen event - Koltuk rezervasyonu başarısız oldu
/// Saga State Machine tarafından consume edilir
/// </summary>
public class FlightReservationFailedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
}
