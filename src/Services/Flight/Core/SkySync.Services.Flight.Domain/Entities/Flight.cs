using SkySync.Services.Flight.Domain.Enums;

namespace SkySync.Services.Flight.Domain.Entities;

public class Flight : BaseEntity
{
    public Guid AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }
    public string FlightNumber { get; set; }
    public string Departure { get; set; }
    public string Destination { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; } // Eklendi
    public decimal BasePrice { get; set; }
    public FlightStatus Status { get; set; } 
    public ICollection<Seat> Seats { get; set; }
}