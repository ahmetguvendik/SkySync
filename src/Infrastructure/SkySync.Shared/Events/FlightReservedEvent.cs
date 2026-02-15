namespace SkySync.Shared.Events;

/// <summary>
/// Flight Service'den gelen event - Koltuk başarıyla rezerve edildi
/// </summary>
public class FlightReservedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReservedAt { get; set; }

    // Rezervasyon maili için gerekli uçuş bilgileri
    public string FlightNumber { get; set; } = string.Empty;
    public string Departure { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
