namespace SkySync.Shared.Events;

public class FlightCreatedEvent
{
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; }
    public string Departure { get; set; }
    public string Destination { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal BasePrice { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
