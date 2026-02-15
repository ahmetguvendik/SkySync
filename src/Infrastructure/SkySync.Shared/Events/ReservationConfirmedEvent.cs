namespace SkySync.Shared.Events;

/// <summary>
/// Notification Service'e gönderilecek event - Rezervasyon onaylandı
/// Saga State Machine tarafından publish edilir
/// </summary>
public class ReservationConfirmedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerSurname { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime ConfirmedAt { get; set; }

    // Yolcuya giden mail için uçuş bilgileri
    public string FlightNumber { get; set; } = string.Empty;
    public string Departure { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
