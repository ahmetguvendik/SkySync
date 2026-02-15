namespace SkySync.Services.Flight.Domain.Entities;

public class Seat : BaseEntity
{
    public string SeatNumber { get; set; }
    public bool IsReserved { get; set; }
    public decimal Price { get; set; } // Uçuştan bağımsız koltuk fiyatı
    public Guid? UserId { get; set; }
    public Guid FlightId { get; set; }
    public Flight Flight { get; set; }
}