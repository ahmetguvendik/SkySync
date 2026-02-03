namespace SkySync.Services.Reservation.Application.DTOs;

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } // FlightSummary read model (FlightCreatedEvent)
    public string? Departure { get; set; }   // Kalkış (örn. IST)
    public string? Arrival { get; set; }     // Varış (örn. SAW)
    public DateTime? DepartureTime { get; set; } // Uçuş kalkış tarihi/saati
    public string SeatNumber { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; }
    public string PassengerName { get; set; }
    public string PassengerSurname { get; set; }
    public string PassengerEmail { get; set; }
    public DateTime CreatedTime { get; set; }
}
