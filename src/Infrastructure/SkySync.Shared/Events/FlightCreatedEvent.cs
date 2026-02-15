namespace SkySync.Shared.Events;

public class FlightCreatedEvent
{
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Departure { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
