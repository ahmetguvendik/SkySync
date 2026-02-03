namespace SkySync.Services.Flight.Domain.Entities;

/// <summary>
/// Uçak modeli – uçuş oluşturulurken seçilir, koltuk sayısı buna göre belirlenir.
/// </summary>
public class Aircraft
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;  // Örn: "Boeing 737-800", "Airbus A320"
    public int SeatCount { get; set; }                // Toplam koltuk sayısı
}
