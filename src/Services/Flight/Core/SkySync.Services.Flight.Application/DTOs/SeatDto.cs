namespace SkySync.Services.Flight.Application.DTOs;

/// <summary>
/// Seat DTO - Koltuk seçimi için detaylı bilgi
/// </summary>
public class SeatDto
{
    public Guid Id { get; set; }
    public string SeatNumber { get; set; } = string.Empty; // Örn: "12A"
    public bool IsReserved { get; set; }
    public decimal Price { get; set; }
    public Guid? UserId { get; set; } // Rezerve eden kullanıcı (varsa)
}
