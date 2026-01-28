namespace SkySync.Services.Reservation.Application.DTOs;

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } // Flight Service'den Join veya Event ile beslenir
    public string SeatNumber { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; }
    public string PassengerName { get; set; }
    public string PassengerSurname { get; set; }
    public string PassengerEmail { get; set; }
    public DateTime CreatedTime { get; set; }
}
