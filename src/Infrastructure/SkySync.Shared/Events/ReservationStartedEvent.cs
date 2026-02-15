namespace SkySync.Shared.Events;

/// <summary>
/// Rezervasyon başlatıldığında fırlatılan event
/// Saga State Machine'i tetikler
/// </summary>
public class ReservationStartedEvent
{
    public Guid ReservationId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerSurname { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
