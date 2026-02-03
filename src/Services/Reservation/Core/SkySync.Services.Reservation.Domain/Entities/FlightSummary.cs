namespace SkySync.Services.Reservation.Domain.Entities;

/// <summary>
/// Read model: Flight servisinden gelen FlightCreated/FlightUpdated event'leri ile beslenir.
/// Rezervasyon listesinde FlightNumber göstermek için kullanılır.
/// </summary>
public class FlightSummary
{
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string? Departure { get; set; }
    public string? Destination { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public DateTime UpdatedAt { get; set; }
}
