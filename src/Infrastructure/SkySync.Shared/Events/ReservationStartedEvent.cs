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
    public string SeatNumber { get; set; }
    public decimal Price { get; set; }
    public string PassengerName { get; set; }
    public string PassengerSurname { get; set; }
    public string PassengerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
}
